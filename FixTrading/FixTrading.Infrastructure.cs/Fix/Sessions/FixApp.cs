using FixTrading.Application.Interfaces.Fix;
using FixTrading.Application.Interfaces.MarketData;
using FixTrading.Common.Dtos.MarketData;
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
        private readonly Dictionary<string, (decimal? Bid, decimal? Ask)> _symbols = new();         // Her sembol için son bid/ask değerini tutar
        private readonly Dictionary<string, string> _mdReqIdToSymbol = new();  // MDReqID -> Symbol (X mesajlarında grup içinde olmayabiliyor)

        private bool _firstMarketDataLogged;
        private static bool _debugLogged;

        
        public SessionID? CurrentSession => _session;  //dışarıdan aktif session bilgisini okumak için kullanılan property.

        public FixApp(
            IMarketDataBuffer marketDataBuffer,
            IFixMessageHandler fixMessageHandler,
            IOptions<FixMarketDataOptions> fixOptions)
        {
            _marketDataBuffer = marketDataBuffer;
            _fixMessageHandler = fixMessageHandler;
            _fixOptions = fixOptions?.Value ?? new FixMarketDataOptions();
        }


        public void OnCreate(SessionID sessionID)
        {
            Console.WriteLine("FIX oturumu oluşturuldu.");
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
                Console.WriteLine("[FIX] MongoDB buffer flush edildi (son veriler kaydedildi).");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FixApp] OnLogout buffer flush hatası: {ex.Message}");
            }
            finally
            {
                _session = null;
                lock (_mdReqIdToSymbol) { _mdReqIdToSymbol.Clear(); }
            }
        }

        public void ToAdmin(Message message, SessionID sessionID)
        {
            if (message.Header.GetString(Tags.MsgType) == MsgType.LOGON)
            {
                message.SetField(new Username("FINTECHEE"));
                message.SetField(new Password("fintechee123"));
            }
        }

        public void FromAdmin(Message message, SessionID sessionID) { }
        public void ToApp(Message message, SessionID sessionID) { }


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


        // Belirli bir sembol için market data akışını başlatır. Bu metot, server'a market data isteği gönderir.
        public void Subscribe(string symbol)   // Server’a market data isteği gönderir
        {
            while (_session == null)    // Bağlantı kurulana kadar bekle
                Thread.Sleep(100);

            // Önceki çalışan format: slash yok (EURUSD, XAUUSD). SPOTEX bu formatı kullanıyor.
            var fixSymbol = symbol.Trim().ToUpper().Replace("/", "");
            if (_fixOptions.UseSlashSymbolFormat && fixSymbol.Length == 6)
                fixSymbol = $"{fixSymbol[..3]}/{fixSymbol[3..]}"; // EUR/USD alternatifi


            // Market data request oluşturulur
            var mdReqId = Guid.NewGuid().ToString();
            var request = new QuickFix.FIX44.MarketDataRequest(
                new MDReqID(mdReqId),
                new SubscriptionRequestType(    // İstek tipi: önce tam snapshot, sonra güncellemeler
                    SubscriptionRequestType.SNAPSHOT_PLUS_UPDATES),
                new MarketDepth(1)    // Sadece en iyi fiyatları (top of book) istemek için derinlik 1 olarak ayarlanır
            );

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

            var symbolGroup =     // Hangi sembol için veri isteneceğini belirten grup
                new QuickFix.FIX44.MarketDataRequest.NoRelatedSymGroup();
            symbolGroup.SetField(new Symbol(fixSymbol));
            request.AddGroup(symbolGroup);

            lock (_mdReqIdToSymbol)
            {
                _mdReqIdToSymbol[mdReqId] = NormalizeSymbol(fixSymbol);
            }


            Session.SendToTarget(request, _session);         // FIX mesajı aktif session üzerinden server'a gönderilir
        }


        public void OnMessage(       // FIX server ilk tam fiyat bilgisini gönderdiğinde çalışır
            QuickFix.FIX44.MarketDataSnapshotFullRefresh message,
            SessionID sessionID)
        {
            if (!_firstMarketDataLogged) { _firstMarketDataLogged = true; }
            var symbol = message.IsSetField(Tags.Symbol) ? message.GetString(Tags.Symbol)
                : message.IsSetField(Tags.SecurityID) ? message.GetString(Tags.SecurityID)
                : "";
            symbol = NormalizeSymbol(symbol);
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
            Console.WriteLine($"[FIX] MarketDataRequest REDDEDİLDİ (MDReqID={mdReqId}): {reason}");
        }

        // Snapshot sonrası gelen fiyat değişimlerini yakalar
        // (Sadece değişen bid/ask değerleri gelir)
        public void OnMessage(
            QuickFix.FIX44.MarketDataIncrementalRefresh message,
            SessionID sessionID)
        {
            if (!_firstMarketDataLogged) { _firstMarketDataLogged = true; }
            int count = message.GetInt(Tags.NoMDEntries);

            string? symbolFromMessage = null;
            if (message.IsSetField(Tags.Symbol))
                symbolFromMessage = NormalizeSymbol(message.GetString(Tags.Symbol));
            else if (message.IsSetField(Tags.MDReqID))
            {
                var mdReqId = message.GetString(Tags.MDReqID);
                lock (_mdReqIdToSymbol)
                {
                    _mdReqIdToSymbol.TryGetValue(mdReqId, out symbolFromMessage);
                }
            }

            decimal? accBid = null, accAsk = null, accTrade = null;
            var sym = symbolFromMessage ?? "";
            for (int i = 1; i <= count; i++)
            {
                var group =
                    new QuickFix.FIX44
                    .MarketDataIncrementalRefresh
                    .NoMDEntriesGroup();

                message.GetGroup(i, group);

                var symbol = group.IsSetField(Tags.Symbol)
                    ? NormalizeSymbol(group.GetString(Tags.Symbol))
                    : symbolFromMessage ?? "";
                if (!string.IsNullOrEmpty(symbol)) sym = symbol;

                if (string.IsNullOrEmpty(sym)) continue;

                decimal? bid = null, ask = null, trade = null;
                try
                {
                    ParseMdEntry(group, ref bid, ref ask, ref trade);
                    accBid = bid ?? accBid;
                    accAsk = ask ?? accAsk;
                    accTrade = trade ?? accTrade;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[FixApp] ProcessGroup hatası: {ex.Message}");
                }
            }
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
                    if (!_debugLogged) { _debugLogged = true; Console.WriteLine("[FIX] DEBUG: NoMDEntries yok. Raw: " + message.ToString().Replace('\x01', '|').Substring(0, Math.Min(300, message.ToString().Length))); }
                    Render(symbol, null, null);
                    return;
                }
                int count = message.GetInt(Tags.NoMDEntries);
                if (count <= 0)
                {
                    if (!_debugLogged) { _debugLogged = true; Console.WriteLine($"[FIX] DEBUG: NoMDEntries=0, Symbol={symbol}"); }
                    Render(symbol, null, null);
                    return;
                }

                for (int i = 1; i <= count; i++)
                {
                    var group = new QuickFix.FIX44.MarketDataSnapshotFullRefresh.NoMDEntriesGroup();
                    message.GetGroup(i, group);
                    if (!_debugLogged && i == 1)
                    {
                        _debugLogged = true;
                        try
                        {
                            var hasType = group.IsSetField(Tags.MDEntryType);
                            var hasPx = group.IsSetField(Tags.MDEntryPx);
                            var typeVal = hasType ? ((int)group.GetChar(Tags.MDEntryType)).ToString() : "yok";
                            var pxVal = hasPx ? group.GetDecimal(Tags.MDEntryPx).ToString() : "yok";
                            Console.WriteLine($"[FIX] DEBUG: NoMDEntries={count}, Symbol={symbol}, MDEntryType(269)={typeVal}, MDEntryPx(270)={pxVal}");
                        }
                        catch { }
                    }
                    ParseMdEntry(group, ref bid, ref ask, ref trade);
                }

                ApplyTradeFallback(ref bid, ref ask, trade);
            }
            catch (Exception ex)
            {
                if (!_debugLogged) { _debugLogged = true; Console.WriteLine($"[FixApp] ProcessMarketData hatası: {ex.Message}\n{ex.StackTrace}"); }
                else Console.WriteLine($"[FixApp] ProcessMarketData: {ex.Message}");
            }

            Render(symbol, bid, ask);
        }


        // Incremental mesajdaki tek bir bid/ask değişimini işler
        private void ProcessGroup(string symbol, Group group)
        {
            decimal? bid = null;
            decimal? ask = null;
            decimal? trade = null;

            try
            {
                ParseMdEntry(group, ref bid, ref ask, ref trade);
                ApplyTradeFallback(ref bid, ref ask, trade);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FixApp] ProcessGroup hatası: {ex.Message}");
            }

            Render(symbol, bid, ask);
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
            (decimal? bid, decimal? ask) data;
            lock (_lock)
            {
                if (!_symbols.ContainsKey(symbol))
                    _symbols[symbol] = (bid, ask);
                else
                {
                    var existing = _symbols[symbol];
                    _symbols[symbol] = (bid ?? existing.Bid, ask ?? existing.Ask);
                }
                data = _symbols[symbol];
            }

            var bidVal = data.bid ?? 0;
            var askVal = data.ask ?? 0;
            var (mid, spread) = PricingCalculator.FromBidAsk(bidVal, askVal);
            var utcNow = DateTime.UtcNow;
            var turkeyTime = utcNow + TimeSpan.FromHours(3);
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
    }
}
