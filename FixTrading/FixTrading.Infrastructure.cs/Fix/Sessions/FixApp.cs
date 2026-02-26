using FixTrading.Application.Interfaces.MarketData;
using FixTrading.Infrastructure.Fix;
using Microsoft.Extensions.Options;
using QuickFix;
using QuickFix.Fields;

namespace FixTrading.Infrastructure.Fix.Sessions
{
    public class FixApp : MessageCracker, IApplication
    // FIX mesajlarını işleyen ana uygulama sınıfı. Gelen mesajları işler ve konsola yazdırır. 
    //IApplication → FIX session event’lerini yönetir
    // MessageCracker → Gelen mesaj tipini ayırır
    {
        private SessionID? _session;    // Aktif FIX oturumunu tutar
        private readonly object _lock = new object();

        private readonly IMarketDataBuffer _marketDataBuffer;
        private readonly FixMarketDataOptions _fixOptions;
        private readonly Dictionary<string, (decimal? Bid, decimal? Ask)> _symbols = new();
        private bool _firstMarketDataLogged;
        private int _marketDataMsgCount;

        public SessionID? CurrentSession => _session;

        public FixApp(IMarketDataBuffer marketDataBuffer, IOptions<FixMarketDataOptions> fixOptions)
        {
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

        public void OnLogout(SessionID sessionID)
        {
            Console.WriteLine("FIX bağlantısı kapatıldı.");
            _session = null;
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



        
        public void Subscribe(string symbol)   // Sembola göre fiyat akışını başlatır
        {
            while (_session == null)    // Bağlantı kurulana kadar bekle
                Thread.Sleep(100);

            // Önceki çalışan format: slash yok (EURUSD, XAUUSD). SPOTEX bu formatı kullanıyor.
            var fixSymbol = symbol.Trim().ToUpper().Replace("/", "");
            if (_fixOptions.UseSlashSymbolFormat && fixSymbol.Length == 6)
                fixSymbol = $"{fixSymbol[..3]}/{fixSymbol[3..]}"; // EUR/USD alternatifi

            var request = new QuickFix.FIX44.MarketDataRequest(
                new MDReqID(Guid.NewGuid().ToString()),
                new SubscriptionRequestType(
                    SubscriptionRequestType.SNAPSHOT_PLUS_UPDATES),
                new MarketDepth(1)
            );

            request.Set(new MDUpdateType(0));
            request.Set(new AggregatedBook(true));   

            var bidGroup =         // BID (alış) fiyatlarını istemek için grup oluşturulur
                new QuickFix.FIX44.MarketDataRequest.NoMDEntryTypesGroup();
            bidGroup.SetField(new MDEntryType(MDEntryType.BID));
            request.AddGroup(bidGroup);

            var askGroup =         // OFFER (satış) fiyatlarını istemek için grup oluşturulur
                new QuickFix.FIX44.MarketDataRequest.NoMDEntryTypesGroup();
            askGroup.SetField(new MDEntryType(MDEntryType.OFFER));
            request.AddGroup(askGroup);

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
            ProcessMarketData(message.GetString(Tags.Symbol), message);
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

            int count = message.GetInt(Tags.NoMDEntries);

            // Her fiyat kaydını tek tek işle
            for (int i = 1; i <= count; i++)
            {
                var group =
                    new QuickFix.FIX44
                    .MarketDataSnapshotFullRefresh
                    .NoMDEntriesGroup();

                message.GetGroup(i, group);

                var type = group.GetChar(Tags.MDEntryType);
                var price = group.GetDecimal(Tags.MDEntryPx);

                if (type == MDEntryType.BID)
                    bid = price;

                if (type == MDEntryType.OFFER)
                    ask = price;
            }

            Render(symbol, bid, ask);             // İşlenen veriyi ekrana yazdır
        }


        // Incremental mesajdaki tek bir bid/ask değişimini işler
        private void ProcessGroup(string symbol, Group group)
        {
            decimal? bid = null;
            decimal? ask = null;

            var type = group.GetChar(Tags.MDEntryType);
            var price = group.GetDecimal(Tags.MDEntryPx);

            // Bid güncellendiyse
            if (type == MDEntryType.BID)
                bid = price;

            // Ask güncellendiyse
            if (type == MDEntryType.OFFER)
                ask = price;

            // Güncel fiyatı ekrana yansıt
            Render(symbol, bid, ask);
        }


        private static string NormalizeSymbol(string symbol) => symbol.Trim().ToUpper().Replace("/", "");

        /// <summary>
        /// 1) Konsol: Her tick'te anlık canlı akış (Mongo'dan bağımsız).
        /// 2) Mongo buffer: Sadece geçerli veriyi buffer'a ekler; flush 60 sn'de bir ayrı çalışır.
        /// EUR/USD ve EURUSD aynı sembol olarak işlenir.
        /// </summary>
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

            // 1) KONSOL: Anlık real-time akış - her tick'te hemen yazdır (Mongo'dan bağımsız)
            var bidText = data.bid?.ToString("0.####") ?? "-";
            var askText = data.ask?.ToString("0.####") ?? "-";
            Console.WriteLine($"{symbol} - {bidText} / {askText}");

            // 2) MONGO BUFFER: Sembol başına son değer (60 sn'de bir flush ile yazılacak)
            if (data.bid.HasValue && data.ask.HasValue && data.bid.Value > 0 && data.ask.Value > 0)
            {
                try
                {
                    _marketDataBuffer.Add(symbol, data.bid.Value, data.ask.Value);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[FixApp] Buffer ekleme hatası: {ex.Message}");
                }
            }
        }
    }
}
