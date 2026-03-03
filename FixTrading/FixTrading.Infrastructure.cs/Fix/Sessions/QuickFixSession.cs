using FixTrading.Application.Interfaces.Fix;
using QuickFix;
using QuickFix.Logger;
using QuickFix.Store;
using QuickFix.Transport;

namespace FixTrading.Infrastructure.Fix.Sessions
{

    //FIX sunucusuna bağlanır, bağlantıyı başlatır, durdurur ve sembol subscribe işlemini yönetir.
    public class QuickFixSession : IFixSession
    {
        private readonly FixApp _app;
        private readonly IInitiator _initiator;

        public bool IsConnected => _app.CurrentSession != null;    //Eğer aktif bir FIX oturumu varsa true döndür.

        public QuickFixSession(FixApp app)
        {
            _app = app;

            // Çalışan uygulamanın output klasöründeki fix.cfg dosyasını kullan
            var configPath = Path.Combine(AppContext.BaseDirectory, "fix.cfg");
            var settings = new SessionSettings(configPath);   
            var storeFactory = new FileStoreFactory(settings);
            var logFactory = new FileLogFactory(settings);

            //Initiator: FIX bağlantısını başlatan yapı.
            //FIX bağlantısını yöneten SocketInitiator oluşturulur. Bu sınıf, FIX sunucusuna TCP/IP üzerinden bağlanmak için kullanılır
            _initiator = new SocketInitiator(    
                _app,
                storeFactory,
                settings,
                logFactory
            );
        }

        public void Start() => _initiator.Start();  //FIX bağlantısını başlatır. 
        public void Stop() => _initiator.Stop();   //FIX bağlantısını durdurur.
        public void Subscribe(string symbol) => _app.Subscribe(symbol);    //Verilen sembol için Market Data isteği gönder.
    }
}
