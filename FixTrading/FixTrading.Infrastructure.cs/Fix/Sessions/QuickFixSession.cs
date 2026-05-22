using System.Globalization;
using FixTrading.Application.Interfaces.Fix;
using FixTrading.Common.Dtos.Options;
using Microsoft.Extensions.Options;
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

        public QuickFixSession(FixApp app, IOptions<FixMarketDataOptions> fixOptions)
        {
            _app = app;

            // Çalışan uygulamanın output klasöründeki fix.cfg dosyasını kullan
            var configPath = Path.Combine(AppContext.BaseDirectory, "fix.cfg");
            var settings = new SessionSettings(configPath);
            ApplySocketOverrides(settings, fixOptions.Value);
            EnsureFixDataDirectories(settings);
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

        // QuickFIX dosya log/store yollari cfg'de goreceli ise BaseDirectory altinda acilir; klasor yoksa olusturulmazsa hem log hem worker teşhisi kırılır.
        private static void EnsureFixDataDirectories(SessionSettings settings)
        {
            try
            {
                var defaults = settings.Get();
                if (defaults.Has("FileLogPath"))
                    Directory.CreateDirectory(ToFixPath(defaults.GetString("FileLogPath")));
                if (defaults.Has("FileStorePath"))
                    Directory.CreateDirectory(ToFixPath(defaults.GetString("FileStorePath")));
            }
            catch
            {
                // cfg okunamazsa SocketInitiator yine de hatayi bildirir
            }
        }

        private static string ToFixPath(string pathFromCfg)
        {
            pathFromCfg = pathFromCfg.Trim();
            if (string.IsNullOrEmpty(pathFromCfg))
                return AppContext.BaseDirectory;
            return Path.IsPathRooted(pathFromCfg)
                ? pathFromCfg
                : Path.Combine(AppContext.BaseDirectory, pathFromCfg);
        }

        private static void ApplySocketOverrides(SessionSettings settings, FixMarketDataOptions opt)
        {
            var host = opt.SocketConnectHost?.Trim();
            var hasHost = !string.IsNullOrEmpty(host);
            int? port = opt.SocketConnectPort is int p && p > 0 ? p : null;
            var hasPort = port.HasValue;
            if (!hasHost && !hasPort)
                return;

            foreach (SessionID sessionId in settings.GetSessions())
            {
                var d = settings.Get(sessionId);
                if (hasHost)
                    d.SetString("SocketConnectHost", host!);
                if (hasPort)
                    d.SetString("SocketConnectPort", port!.Value.ToString(CultureInfo.InvariantCulture));
            }
        }

        public void Start() => _initiator.Start();  //FIX bağlantısını başlatır. 
        public void Stop() => _initiator.Stop();   //FIX bağlantısını durdurur.
        public void Subscribe(string symbol) => _app.Subscribe(symbol);    //Verilen sembol için Market Data isteği gönder.

        public void Unsubscribe(string symbol) => _app.Unsubscribe(symbol);  //Market Data akışını durdurur.

        public bool IsSubscribed(string symbol) => _app.IsSubscribed(symbol);  //Sembol için aktif abonelik var mı?
    }
}
