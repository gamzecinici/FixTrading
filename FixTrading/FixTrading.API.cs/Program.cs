using FixTrading.API;

// Konsol çıktısının hemen görünmesi için
Console.Out.Flush();
Console.WriteLine("[FixTrading] Uygulama başlatılıyor...");

var builder = WebApplication.CreateBuilder(args);

// Startup üzerinden servisleri kaydet
var startup = new Startup(builder.Configuration);
startup.ConfigureServices(builder.Services);

// Varsayılan logları koru; sadece console ekleyerek tüm çıktıların görünmesini sağla
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

WebApplication app;
try
{
    app = builder.Build();
}
catch (Exception ex)
{
    Console.WriteLine("[FixTrading] Uygulama oluşturulurken hata: " + ex.Message);
    Console.WriteLine(ex.StackTrace);
    throw;
}

// Startup üzerinden pipeline'ı yapılandır
startup.Configure(app);

Console.WriteLine("[FixTrading] Web sunucu başlıyor.");
app.Run();
