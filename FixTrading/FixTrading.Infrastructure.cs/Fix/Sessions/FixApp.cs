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
    // FIX mesajlarını yakalayan ve işleyen ana sınıf
    // IApplication → FIX bağlantı olaylarını yönetir (logon, logout vb.)
    // MessageCracker → Gelen mesaj tipine göre doğru OnMessage metodunu çağırır
    {
        private SessionID? _session;    // Aktif FIX oturumunu tutar
        private readonly object _lock = new object();    // Çoklu thread'lerde sembol verilerine güvenli erişim için kilit nesnesi

        private readonly IMarketDataSubject _marketDataSubject;    // Observer pattern için kullanılan Subject. Yeni fiyat geldiğinde tüm Observer'lara bildirim gönderir.
        private readonly IMarketDataBuffer _marketDataBuffer;      // FIX disconnect sırasında buffer'ı flush etmek için
        private readonly FixMarketDataOptions _fixOptions;
        private readonly Dictionary<string, (decimal? Bid, decimal? Ask)> _symbols = new();         // Her sembol için son bid/ask değerini tutar

        private bool _firstMarketDataLogged;
        private int _marketDataMsgCount;
        private static bool _debugLogged;

        
        public SessionID? CurrentSession => _session;  //dışarıdan aktif session bilgisini okumak için kullanılan property.

        public FixApp(IMarketDataSubject marketDataSubject, IMarketDataBuffer marketDataBuffer, IOptions<FixMarketDataOptions> fixOptions)
        {
            _marketDataSubject = marketDataSubject;
            _marketDataBuffer = marketDataBuffer;
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
                var msgType = message.Header.GetString(Tags.MsgType);
                if (msgType == "W" || msgType == "X")
                {
                    if (++_marketDataMsgCount <= 5)
                        Console.WriteLine($"[FIX] Market data mesajı #{_marketDataMsgCount}: MsgType={msgType}");
                }
                else
                    Console.WriteLine($"[FIX] Gelen mesaj: MsgType={msgType}");
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
            var request = new QuickFix.FIX44.MarketDataRequest(
                new MDReqID(Guid.NewGuid().ToString()),   // Her istek için benzersiz bir ID oluşturulur
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

            Console.WriteLine($"Subscribe gönderildi: {fixSymbol}");

            Session.SendToTarget(request, _session);         // FIX mesajı aktif session üzerinden server'a gönderilir
        }


        public void OnMessage(       // FIX server ilk tam fiyat bilgisini gönderdiğinde çalışır
            QuickFix.FIX44.MarketDataSnapshotFullRefresh message,
            SessionID sessionID)
        {
            if (!_firstMarketDataLogged) { _firstMarketDataLogged = true; Console.WriteLine("[FIX] İlk MarketDataSnapshotFullRefresh (W) alındı."); }
            var symbol = message.IsSetField(Tags.Symbol) ? message.GetString(Tags.Symbol)
                : message.IsSetField(Tags.SecurityID) ? message.GetString(Tags.SecurityID)
                : "";
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
            if (!_firstMarketDataLogged) { _firstMarketDataLogged = true; Console.WriteLine("[FIX] İlk MarketDataIncrementalRefresh (X) alındı."); }
            int count = message.GetInt(Tags.NoMDEntries);

            for (int i = 1; i <= count; i++)
            {
                var group =
                    new QuickFix.FIX44
                    .MarketDataIncrementalRefresh
                    .NoMDEntriesGroup();

                message.GetGroup(i, group);

                var symbol = group.GetString(Tags.Symbol);
                ProcessGroup(symbol, group);
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


        // Verilen sembol, bid ve ask fiyatlarını işler ve Observer pattern ile tüm dinleyicilere bildirir.
        // (Konsol, MongoDB buffer, Redis gibi Observer'lar Notify ile bilgilendirilir)
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
                    _symbols[symbol] = (bid ?? existing.Bid, ask ?? existing.Ask);   //yeni gelen bid veya ask null değilse güncelle, null ise eski değeri koru
                }
                data = _symbols[symbol];
            }

            // 1) KONSOL: Anlık real-time akış - her tick'te hemen yazdır (Mongo'dan bağımsız)
            // 2) MONGO BUFFER: 60 sn boyunca biriken tüm veriler toplu yazılacak
            // 3) REDIS: En son fiyat her tick'te güncellenir (Latest Price API için)

            var bidVal = data.bid ?? 0;
            var askVal = data.ask ?? 0;
            var (mid, spread) = PricingCalculator.FromBidAsk(bidVal, askVal);   // Mid ve spread hesaplanır
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
            _marketDataSubject.Notify(dto);        // Yeni fiyat geldiğinde tüm Observer'lara bildirim gönderilir
        }
    }
}
