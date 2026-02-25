using FixTrading.Application.Interfaces.Fix;
using QuickFix;
using QuickFix.Logger;
using QuickFix.Store;
using QuickFix.Transport;

namespace FixTrading.Infrastructure.Fix.Sessions
{
    public class QuickFixSession : IFixSession
    {
        private readonly FixApp _app;
        private readonly IInitiator _initiator;

        public bool IsConnected => _app.CurrentSession != null;

        public QuickFixSession(FixApp app)
        {
            _app = app;

            // Çalışan uygulamanın output klasöründeki fix.cfg dosyasını kullan
            var configPath = Path.Combine(AppContext.BaseDirectory, "fix.cfg");
            var settings = new SessionSettings(configPath);
            var storeFactory = new FileStoreFactory(settings);
            var logFactory = new FileLogFactory(settings);

            _initiator = new SocketInitiator(
                _app,
                storeFactory,
                settings,
                logFactory
            );
        }

        public void Start() => _initiator.Start();
        public void Stop() => _initiator.Stop();
        public void Subscribe(string symbol) => _app.Subscribe(symbol);
    }
}
