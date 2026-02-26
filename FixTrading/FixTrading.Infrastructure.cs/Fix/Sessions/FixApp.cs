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

        private readonly Dictionary<string, (decimal? Bid, decimal? Ask)> _symbols = new();

        public SessionID? CurrentSession => _session;


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
            Console.WriteLine("MESAJ GELDİ: " + message);
            try
            {
                Crack(message, sessionID);
            }
            catch (QuickFix.UnsupportedMessageType)
            {
            }
        }



        
        public void Subscribe(string symbol)   // Sembola göre fiyat akışını başlatır
        {
            while (_session == null)    // Bağlantı kurulana kadar bekle
                Thread.Sleep(100);

            // Veritabanından gelen sembolü temizle ve büyük harfe çevir
            var fixSymbol = symbol.Trim().ToUpper().Replace("/", "");

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
            ProcessMarketData(message.GetString(Tags.Symbol), message);
        }



        // Snapshot sonrası gelen fiyat değişimlerini yakalar
        // (Sadece değişen bid/ask değerleri gelir)
        public void OnMessage(
            QuickFix.FIX44.MarketDataIncrementalRefresh message,
            SessionID sessionID)
        {
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


        // Fiyatları console'da 'SYMBOL - BID / ASK' formatında gösterir
        private void Render(string symbol, decimal? bid, decimal? ask)
        {
            lock (_lock)
            {
                // En son bilinen bid/ask değerlerini sakla
                if (!_symbols.ContainsKey(symbol))
                {
                    _symbols[symbol] = (bid, ask);
                }
                else
                {
                    var existing = _symbols[symbol];
                    _symbols[symbol] = (
                        bid ?? existing.Bid,
                        ask ?? existing.Ask
                    );
                }

                var data = _symbols[symbol];
                var bidText = data.Bid?.ToString("0.####") ?? "-";
                var askText = data.Ask?.ToString("0.####") ?? "-";

                Console.WriteLine($"{symbol} - {bidText} / {askText}");
            }
        }
    }
}
