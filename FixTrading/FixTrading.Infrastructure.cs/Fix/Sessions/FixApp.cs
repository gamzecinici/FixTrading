using System.Collections.Concurrent;
using FixTrading.Application.Interfaces;
using FixTrading.Application.Interfaces.Fix;
using FixTrading.Application.Interfaces.MarketData;
using FixTrading.Common.Dtos.MarketData;
using FixTrading.Common.Dtos.Options;
using FixTrading.Common.Pricing;
using FixTrading.Infrastructure.Fix;
using Microsoft.Extensions.Options;
using QuickFix;
using QuickFix.Fields;

namespace FixTrading.Infrastructure.Fix.Sessions
{
    public class FixApp : MessageCracker, IApplication
    {
        private SessionID? _session;
        private readonly object _lock = new object();

        private readonly IMarketDataBuffer _marketDataBuffer;
        private readonly IFixMessageHandler _fixMessageHandler;
        private readonly FixMarketDataOptions _fixOptions;
        private readonly ISystemParameterService _systemParameterService;
        private readonly IMarketHubService _marketHubService;
        private readonly Dictionary<string, (decimal? Bid, decimal? Ask)> _symbols = new();         // Her sembol için son bid/ask değerini tutar
        private readonly Dictionary<string, string> _mdReqIdToSymbol = new();  // MDReqID -> Symbol (X mesajlarında grup içinde olmayabiliyor)
        private readonly ConcurrentDictionary<string, string> _symbolToMdReqId = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, int> _emptySnapshotCounts = new(StringComparer.OrdinalIgnoreCase);
        private static int _reqCounter = 0;

        // ── Aktif FIX abonelikleri (whitelist) ─────────────────────────────────────────────
        // Admin "Enstrüman sil" dediğinde Unsubscribe() bu setten sembolü düşürür.
        // Sunucu unsubscribe sonrası yine de tick gönderebilir; Render() bu sembolü sette görmüyorsa
        // tick hiç işlenmez → Redis/InMemory tekrar doldurulmaz, UI'da "silinmiş" sembol geri gelmez.
        // Subscribe() her başarılı abonelikte sembolü buraya ekler (NormalizeSymbol ile, örn. EURUSD).
        // Abonelik REDDEDİLDİĞİNDE de bu setten çıkarılmalıdır.
        private readonly HashSet<string> _activeSymbols = new(StringComparer.OrdinalIgnoreCase);
        private string? _accountId;

        public SessionID? CurrentSession => _session;  //dışarıdan aktif session bilgisini okumak için kullanılan property.

        public FixApp(
            IMarketDataBuffer marketDataBuffer,
            IFixMessageHandler fixMessageHandler,
            IOptions<FixMarketDataOptions> fixOptions,
            ISystemParameterService systemParameterService,
            IMarketHubService marketHubService)
        {
            _marketDataBuffer = marketDataBuffer;
            _fixMessageHandler = fixMessageHandler;
            _fixOptions = fixOptions?.Value ?? new FixMarketDataOptions();
            _systemParameterService = systemParameterService;
            _marketHubService = marketHubService;

            LoadDefaultAccountId();
        }


        public void OnCreate(SessionID sessionID)
        {
            LoadSessionAccountId(sessionID);
        }


        public void OnLogon(SessionID sessionID)
        {
            Console.WriteLine("FIX bağlantısı başarılı.");
            _session = sessionID;
        }


        // FIX bağlantısı kapatıldığında çalışır. Buffer'daki verileri MongoDB'ye kaydeder ve session bilgisini temizler.
        public void OnLogout(SessionID sessionID)
        {
            try
            {
                Console.WriteLine("FIX bağlantısı kapatıldı.");
                _marketDataBuffer.Flush();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FixApp] OnLogout buffer flush hatası: {ex.Message}");
            }
            finally
            {
                _session = null;
                lock (_mdReqIdToSymbol) { _mdReqIdToSymbol.Clear(); }
                _symbolToMdReqId.Clear();
            }
        }

        public void ToAdmin(Message message, SessionID sessionID)
        {
            if (message.Header.GetString(Tags.MsgType) == MsgType.LOGON)
            {
                var username = _fixOptions.Username;
                var password = _fixOptions.Password;

                ApplyFixAppCredentials(ref username, ref password);

                message.SetField(new Username(username));
                message.SetField(new Password(password));
            }
        }

        public void FromAdmin(Message message, SessionID sessionID)
        {
            // QuickFIX callback is intentionally unused; admin messages are handled by the engine.
        }
        public void ToApp(Message message, SessionID sessionID)
        {
            // Bazı sunucular uygulama mesajlarında Account bilgisini zorunlu tutabiliyor (fix.cfg'den okunur).
            if (!string.IsNullOrEmpty(_accountId))
            {
                message.SetField(new Account(_accountId));
            }
        }


        // Gelen uygulama mesajlarını işler. Her mesaj geldiğinde çalışır.
        public void FromApp(Message message, SessionID sessionID)
        {
            try
            {
                Crack(message, sessionID);
            }
            catch (QuickFix.UnsupportedMessageType)
            {
                var msgType = message?.Header?.GetString(Tags.MsgType) ?? "?";
                Console.WriteLine($"[FIX] Desteklenmeyen mesaj tipi (handler yok): MsgType={msgType}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FixApp] Mesaj işleme hatası: {ex.Message}");
            }
        }


        // Sembol için aktif bir market data aboneliği var mı?
        public bool IsSubscribed(string symbol)
        {
            var normalized = NormalizeSymbol(symbol);
            return _symbolToMdReqId.ContainsKey(normalized);
        }

        // Belirli bir sembol için market data akışını başlatır. Bu metot, server'a market data isteği gönderir.
        public void Subscribe(string symbol)   // Server’a market data isteği gönderir
        {
            while (_session == null)    // Bağlantı kurulana kadar bekle
                Thread.Sleep(100);

            // Idempotent: sembol için zaten aktif bir abonelik varsa tekrar göndermeyiz.
            // Aynı sembole çift abone olunması broker tarafından reddedilebilir veya MDReqID karmaşası yaratır.
            var preNormalized = NormalizeSymbol(symbol);
            if (_symbolToMdReqId.TryGetValue(preNormalized, out var existingMdReqId))
            {
                Console.WriteLine($"[FIX] Zaten abone: {preNormalized} (MDReqID={existingMdReqId}). Yeni istek gönderilmedi.");
                lock (_lock) { _activeSymbols.Add(preNormalized); }
                return;
            }

            // Spotex gibi sağlayıcılar genellikle "EUR/USD" formatını (slash ile) bekler.
            // Eğer UseSlashSymbolFormat aktifse ve sembol 6 karakterse (örneğin GBPTRY), araya slash eklenir.
            var fixSymbol = symbol.Trim().ToUpper().Replace("/", "");
            if (_fixOptions.UseSlashSymbolFormat && fixSymbol.Length == 6)
                fixSymbol = $"{fixSymbol[..3]}/{fixSymbol[3..]}"; // GBP/TRY formatına dönüştürür


            // Market data request oluşturulur
            var mdReqId = "REQ" + Interlocked.Increment(ref _reqCounter);
            var request = new QuickFix.FIX44.MarketDataRequest(
                new MDReqID(mdReqId),
                new SubscriptionRequestType(    // İstek tipi: önce tam snapshot, sonra güncellemeler
                    SubscriptionRequestType.SNAPSHOT_PLUS_UPDATES),
                new MarketDepth(1)    // Sadece en iyi fiyatları (top of book) istemek için derinlik 1 olarak ayarlanır
            );

            request.Set(new MDUpdateType(0)); // 0=Full, 1=Incremental.
            request.Set(new AggregatedBook(true));   

            var bidGroup = new QuickFix.FIX44.MarketDataRequest.NoMDEntryTypesGroup();
            bidGroup.SetField(new MDEntryType(MDEntryType.BID));
            request.AddGroup(bidGroup);

            var askGroup = new QuickFix.FIX44.MarketDataRequest.NoMDEntryTypesGroup();
            askGroup.SetField(new MDEntryType(MDEntryType.OFFER));
            request.AddGroup(askGroup);

            var symbolGroup =     // Hangi sembol için veri isteneceğini belirten grup
                new QuickFix.FIX44.MarketDataRequest.NoRelatedSymGroup();
            symbolGroup.SetField(new Symbol(fixSymbol));
            request.AddGroup(symbolGroup);

            var normalizedForMap = NormalizeSymbol(fixSymbol);
            lock (_mdReqIdToSymbol)
            {
                _mdReqIdToSymbol[mdReqId] = normalizedForMap;
            }

            _symbolToMdReqId[normalizedForMap] = mdReqId;

            // Abonelik sunucuya gitmeden önce sembolü aktif listeye alıyoruz.
            // Böylece dönüş snapshot/incremental mesajları Render()'da reddedilmez.
            lock (_lock)
            {
                _activeSymbols.Add(normalizedForMap);
            }

            Console.WriteLine($"[FIX] Abone olunuyor: {fixSymbol} (MDReqID={mdReqId})");
            Session.SendToTarget(request, _session);         // FIX mesajı aktif session üzerinden server'a gönderilir
        }

        // Belirli bir sembol için market data akışını durdurur. Bu metot, server'a market data aboneliğini iptal etme isteği gönderir.
        public void Unsubscribe(string symbol)
        {
            // FIX oturumu kapalıysa sunucuya iptal mesajı gönderilemez; yine de yerel durumu temizliyoruz.
            // Böylece uygulama yeniden bağlanınca eski sembol "hayalet abonelik" ile işlenmez.
            if (_session == null)
            {
                var normEarly = NormalizeSymbol(symbol);
                _symbolToMdReqId.TryRemove(normEarly, out _);
                lock (_lock)
                {
                    _symbols.Remove(normEarly);
                    _activeSymbols.Remove(normEarly);
                }
                return;
            }

            // Sembolü slash'sız hale getirip, ayara göre tekrar formatlıyoruz.
            var fixSymbol = symbol.Trim().ToUpper().Replace("/", "");
            if (_fixOptions.UseSlashSymbolFormat && fixSymbol.Length == 6)
                fixSymbol = $"{fixSymbol[..3]}/{fixSymbol[3..]}";

            // Market data aboneliğini iptal etmek için önce sembole karşılık gelen MDReqID bulunur, sonra bu ID kullanılarak iptal isteği gönderilir.
            var normalized = NormalizeSymbol(fixSymbol);
            if (!_symbolToMdReqId.TryRemove(normalized, out var mdReqId))
                mdReqId = null;

            if (mdReqId is not null)
            {
                lock (_mdReqIdToSymbol)
                {
                    _mdReqIdToSymbol.Remove(mdReqId);
                }
                SendUnsubscribeRequest(normalized, mdReqId);
            }

            // Yerel bid/ask önbelleğinden ve aktif abonelik listesinden çıkar.
            // Bundan sonra bu sembole gelen tickler Render() içinde _activeSymbols kontrolüyle elenir.
            lock (_lock)
            {
                _symbols.Remove(normalized);
                _activeSymbols.Remove(normalized);
            }
        }

        private void SendUnsubscribeRequest(string symbol, string mdReqId)
        {
            if (_session == null) return;

            var fixSymbol = symbol.Trim().ToUpper().Replace("/", "");
            if (_fixOptions.UseSlashSymbolFormat && fixSymbol.Length == 6)
                fixSymbol = $"{fixSymbol[..3]}/{fixSymbol[3..]}";

            // Market data aboneliğini iptal etmek için MarketDataRequest mesajı oluşturulur ve gönderilir.
            var request = new QuickFix.FIX44.MarketDataRequest(
                new MDReqID(mdReqId),
                new SubscriptionRequestType(SubscriptionRequestType.DISABLE_PREVIOUS_SNAPSHOT_PLUS_UPDATE_REQUEST),
                new MarketDepth(1));

            request.Set(new MDUpdateType(0));
            request.Set(new AggregatedBook(true));

            var bidGroup = new QuickFix.FIX44.MarketDataRequest.NoMDEntryTypesGroup();
            bidGroup.SetField(new MDEntryType(MDEntryType.BID));
            request.AddGroup(bidGroup);

            var askGroup = new QuickFix.FIX44.MarketDataRequest.NoMDEntryTypesGroup();
            askGroup.SetField(new MDEntryType(MDEntryType.OFFER));
            request.AddGroup(askGroup);

            var tradeGroup = new QuickFix.FIX44.MarketDataRequest.NoMDEntryTypesGroup();
            tradeGroup.SetField(new MDEntryType(MDEntryType.TRADE));
            request.AddGroup(tradeGroup);

            var symbolGroup = new QuickFix.FIX44.MarketDataRequest.NoRelatedSymGroup();
            symbolGroup.SetField(new Symbol(fixSymbol));
            request.AddGroup(symbolGroup);

            Session.SendToTarget(request, _session);
        }


        public void OnMessage(       // FIX server ilk tam fiyat bilgisini gönderdiğinde çalışır
            QuickFix.FIX44.MarketDataSnapshotFullRefresh message,
            SessionID sessionID)
        {
            var symbol = ResolveMessageSymbol(message);
            if (!string.IsNullOrEmpty(symbol) && message.IsSetField(Tags.MDReqID))
            {
                var mdReqId = message.GetString(Tags.MDReqID);
                lock (_mdReqIdToSymbol) { _mdReqIdToSymbol[mdReqId] = symbol; }
            }
            ProcessMarketData(symbol, message);
        }

        public void OnMessage(QuickFix.FIX44.MarketDataRequestReject message, SessionID sessionID)
        {
            var reason = message.IsSetField(Tags.Text) ? message.GetString(Tags.Text) : "(Text yok)";
            var mdReqId = message.IsSetField(Tags.MDReqID) ? message.GetString(Tags.MDReqID) : "?";
            var rejReasonCode = message.IsSetField(Tags.MDReqRejReason) ? message.GetString(Tags.MDReqRejReason) : "yok";
            Console.WriteLine($"[FIX] MarketDataRequest REDDEDİLDİ (MDReqID={mdReqId}, ReasonCode={rejReasonCode}): {reason}");

            // Reddedilen sembolü bul ve temizle
            string? symbol = null;
            lock (_mdReqIdToSymbol)
            {
                if (_mdReqIdToSymbol.TryGetValue(mdReqId, out symbol))
                {
                    _mdReqIdToSymbol.Remove(mdReqId);
                }
            }

            if (symbol != null)
            {
                _symbolToMdReqId.TryRemove(symbol, out _);
                lock (_lock)
                {
                    _activeSymbols.Remove(symbol);
                    _symbols.Remove(symbol);
                }

                // UI'yı bilgilendir (fire-and-forget)
                _ = _marketHubService.NotifySubscriptionRejectedAsync(symbol, reason);
            }
        }

        // Snapshot sonrası gelen fiyat değişimlerini yakalar
        // (Sadece değişen bid/ask değerleri gelir)
        public void OnMessage(
            QuickFix.FIX44.MarketDataIncrementalRefresh message,
            SessionID sessionID)
        {
            var count = message.GetInt(Tags.NoMDEntries);
            var symbolFromMessage = ResolveIncrementalMessageSymbol(message);
            var (sym, accBid, accAsk, accTrade) = ReadIncrementalEntries(message, count, symbolFromMessage);

            if (!string.IsNullOrEmpty(sym))
            {
                ApplyTradeFallback(ref accBid, ref accAsk, accTrade);
                Render(sym, accBid, accAsk);
            }
        }

        // Snapshot mesajındaki tüm bid/ask verilerini okur
        private void ProcessMarketData(string symbol, Message message)
        {
            decimal? bid = null;
            decimal? ask = null;
            decimal? trade = null;

            try
            {
                if (!message.IsSetField(Tags.NoMDEntries))
                {
                    HandleEmptySnapshot(symbol, "NoMDEntries yok");
                    return;
                }
                int count = message.GetInt(Tags.NoMDEntries);
                if (count <= 0)
                {
                    HandleEmptySnapshot(symbol, "NoMDEntries=0");
                    return;
                }

                _emptySnapshotCounts.TryRemove(symbol, out _);
                for (int i = 1; i <= count; i++)
                {
                    var group = new QuickFix.FIX44.MarketDataSnapshotFullRefresh.NoMDEntriesGroup();
                    message.GetGroup(i, group);
                    ParseMdEntry(group, ref bid, ref ask, ref trade);
                }

                ApplyTradeFallback(ref bid, ref ask, trade);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FixApp] ProcessMarketData hatası: {ex.Message}");
            }

            Render(symbol, bid, ask);
        }

        private void HandleEmptySnapshot(string symbol, string reason)
        {
            symbol = NormalizeSymbol(symbol);
            if (string.IsNullOrEmpty(symbol)) return;

            var emptyCount = _emptySnapshotCounts.AddOrUpdate(symbol, 1, (_, current) => current + 1);
            if (emptyCount == 1 || emptyCount % 20 == 0)
            {
                Console.WriteLine($"[FIX] {symbol} icin fiyat gelmedi ({reason}). Provider bos snapshot dondu. Bos sayisi={emptyCount}");
            }
        }

        private static void ParseMdEntry(Group group, ref decimal? bid, ref decimal? ask, ref decimal? trade)
        {
            if (!group.IsSetField(Tags.MDEntryPx)) return;
            var price = group.GetDecimal(Tags.MDEntryPx);
            if (price <= 0) return;

            if (!group.IsSetField(Tags.MDEntryType)) return;
            var type = group.GetChar(Tags.MDEntryType);

            if (type == MDEntryType.BID)
                bid = price;
            else if (type == MDEntryType.OFFER)
                ask = price;
            else if (type == MDEntryType.TRADE)
                trade = price;
        }

        private static void ApplyTradeFallback(ref decimal? bid, ref decimal? ask, decimal? trade)
        {
            if (trade == null) return;
            if (bid == null) bid = trade;
            if (ask == null) ask = trade;
        }

        // Sembolü normalize eder: boşlukları kaldırır, büyük harfe çevirir, slash'ları kaldırır
        private static string NormalizeSymbol(string symbol) => symbol.Trim().ToUpper().Replace("/", "");


        private void Render(string symbol, decimal? bid, decimal? ask)
        {
            symbol = NormalizeSymbol(symbol);

            // ── Kritik: silinmiş / iptal edilmiş enstrüman koruması ─────────────────────
            // _activeSymbols yalnızca Subscribe ile eklenen ve henüz Unsubscribe edilmemiş sembolleri tutar.
            lock (_lock)
            {
                if (!_activeSymbols.Contains(symbol))
                {
                    return;
                }
            }
            
            (decimal? bid, decimal? ask) data;
            lock (_lock)
            {
                if (!_symbols.TryGetValue(symbol, out var existing))
                    _symbols[symbol] = (bid, ask);
                else
                {
                    _symbols[symbol] = (bid ?? existing.Bid, ask ?? existing.Ask);
                }
                data = _symbols[symbol];
            }

            var bidVal = data.bid ?? 0;
            var askVal = data.ask ?? 0;
            if (bidVal <= 0 || askVal <= 0)
            {
                return;
            }

            var (mid, spread) = PricingCalculator.FromBidAsk(bidVal, askVal);
            var u = DateTime.UtcNow;
            var utcNow = new DateTime(u.Year, u.Month, u.Day, u.Hour, u.Minute, u.Second, DateTimeKind.Utc);
            
            // Parametre tablosundan UtcOffset'i al, yoksa varsayılan 3 kullan
            var offset = ResolveUtcOffset();

            var turkeyTime = utcNow + TimeSpan.FromHours(offset);
            var dto = new DtoMarketData
            {
                Symbol = symbol,
                Bid = bidVal,
                Ask = askVal,
                Mid = mid,
                Spread = spread,
                Timestamp = utcNow,
                TimestampFormatted = turkeyTime.ToString("dd.MM.yyyy HH:mm")
            };

            // FIX’ten gelen ve hesaplanan market verisini Application katmanına gönderir,
            // burada alert kontrolü, cache ve diğer iş kuralları devreye girer
            _fixMessageHandler.Handle(dto);     

        }

        private void LoadDefaultAccountId()
        {
            try
            {
                var configPath = Path.Combine(AppContext.BaseDirectory, "fix.cfg");
                if (!File.Exists(configPath))
                    return;

                var settings = new SessionSettings(configPath);
                var dict = settings.Get();
                if (dict.Has("AccountId"))
                    _accountId = dict.GetString("AccountId");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FixApp] AccountId okunamadı: {ex.Message}");
            }
        }

        private void LoadSessionAccountId(SessionID sessionID)
        {
            try
            {
                var configPath = Path.Combine(AppContext.BaseDirectory, "fix.cfg");
                var sessionSettings = new SessionSettings(configPath).Get(sessionID);
                if (sessionSettings.Has("AccountId"))
                    _accountId = sessionSettings.GetString("AccountId");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FixApp] Session AccountId okunamadı: {ex.Message}");
            }
        }

        private void ApplyFixAppCredentials(ref string username, ref string password)
        {
            try
            {
                var config = _systemParameterService.GetConfigAsync("FixApp").GetAwaiter().GetResult();
                if (config == null)
                    return;

                if (config.TryGetValue("Username", out var u) && u != null)
                    username = u;
                if (config.TryGetValue("Password", out var p) && p != null)
                    password = p;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FixApp] dinamik credential okunamadı: {ex.Message}");
            }
        }

        private string? ResolveIncrementalMessageSymbol(Message message)
        {
            var symbol = ResolveMessageSymbol(message);
            if (!string.IsNullOrEmpty(symbol))
                return symbol;

            if (!message.IsSetField(Tags.MDReqID))
                return null;

            var mdReqId = message.GetString(Tags.MDReqID);
            lock (_mdReqIdToSymbol)
            {
                _mdReqIdToSymbol.TryGetValue(mdReqId, out var mappedSymbol);
                return mappedSymbol;
            }
        }

        private static string ResolveMessageSymbol(Message message)
        {
            if (message.IsSetField(Tags.Symbol))
                return NormalizeSymbol(message.GetString(Tags.Symbol));

            return message.IsSetField(Tags.SecurityID)
                ? NormalizeSymbol(message.GetString(Tags.SecurityID))
                : string.Empty;
        }

        private static string ResolveGroupSymbol(Group group, string? fallback)
        {
            if (group.IsSetField(Tags.Symbol))
                return NormalizeSymbol(group.GetString(Tags.Symbol));

            return group.IsSetField(Tags.SecurityID)
                ? NormalizeSymbol(group.GetString(Tags.SecurityID))
                : fallback ?? string.Empty;
        }

        private static (string Symbol, decimal? Bid, decimal? Ask, decimal? Trade) ReadIncrementalEntries(
            QuickFix.FIX44.MarketDataIncrementalRefresh message,
            int count,
            string? symbolFromMessage)
        {
            decimal? accBid = null;
            decimal? accAsk = null;
            decimal? accTrade = null;
            var sym = symbolFromMessage ?? string.Empty;

            for (var i = 1; i <= count; i++)
            {
                var group = new QuickFix.FIX44.MarketDataIncrementalRefresh.NoMDEntriesGroup();
                message.GetGroup(i, group);

                var groupSymbol = ResolveGroupSymbol(group, symbolFromMessage);
                if (!string.IsNullOrEmpty(groupSymbol))
                    sym = groupSymbol;

                if (string.IsNullOrEmpty(sym))
                    continue;

                AccumulateEntry(group, ref accBid, ref accAsk, ref accTrade);
            }

            return (sym, accBid, accAsk, accTrade);
        }

        private static void AccumulateEntry(Group group, ref decimal? accBid, ref decimal? accAsk, ref decimal? accTrade)
        {
            decimal? bid = null;
            decimal? ask = null;
            decimal? trade = null;
            try
            {
                ParseMdEntry(group, ref bid, ref ask, ref trade);
                accBid = bid ?? accBid;
                accAsk = ask ?? accAsk;
                accTrade = trade ?? accTrade;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FixApp] incremental fiyat okuma hatası: {ex.Message}");
            }
        }

        private double ResolveUtcOffset()
        {
            try
            {
                var config = _systemParameterService.GetConfigAsync("FinancialAnalytics").GetAwaiter().GetResult();
                if (config != null &&
                    config.TryGetValue("UtcOffset", out var val) &&
                    double.TryParse(val, out var res))
                    return res;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FixApp] UtcOffset okunamadı: {ex.Message}");
            }

            return 3;
        }
    }
}
