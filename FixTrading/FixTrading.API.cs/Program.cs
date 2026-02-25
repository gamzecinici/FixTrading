using FixTrading.API;

var builder = WebApplication.CreateBuilder(args);

// Startup üzerinden servisleri kaydet
var startup = new Startup(builder.Configuration);
startup.ConfigureServices(builder.Services);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

var app = builder.Build();

// Startup üzerinden pipeline'ı yapılandır
startup.Configure(app);

app.Run();
