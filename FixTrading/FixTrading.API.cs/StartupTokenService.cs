namespace FixTrading.API;

// Uygulama baslatilirken tek bir token uretilir ve bu token uygulamanin her yerinde kullanilabilir.
public sealed class StartupTokenService
{
    public string Token { get; } = Guid.NewGuid().ToString("N");
}
