using FixTrading.API.BackgroundServices;
using FixTrading.API.Controllers;
using FixTrading.API.HealthChecks;
using FixTrading.Common.Dtos.Instrument;
using FixTrading.Application;
using FixTrading.Application.Interfaces.Fix;
using FixTrading.Application.Interfaces.MarketData;
using FixTrading.Domain.Interfaces;
using FixTrading.Infrastructure.Fix;
using FixTrading.Infrastructure.Fix.Sessions;
using FixTrading.Infrastructure.Email;
using FixTrading.Infrastructure.MongoDb;
using FixTrading.Infrastructure.Observers;
using FixTrading.Infrastructure.Pricing;
using FixTrading.Infrastructure.Redis;
using FixTrading.Infrastructure.Stores;
using FixTrading.Persistence;
using FixTrading.Persistence.Repositories;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using StackExchange.Redis;

namespace FixTrading.API;

// Uygulama ilk açıldığında çalışan ayar sınıfı (tüm kurulum burada yapılır)

public class Startup
{
    // Ayarları (appsettings vb.) alır
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    // Servisleri sisteme tanıttığımız yer
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddAuthorization();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        // Katman servisleri - Application
        services.AddApplicationServices();

        // Katman servisleri - Persistence (DB)
        var connectionString = Configuration.GetConnectionString("DefaultConnection")!;
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));
        services.AddScoped<IInstrumentRepository, InstrumentRepository>();
        services.AddScoped<IPricingLimitsRepository, PricingLimitsRepository>();

        // Pricing limits cache (singleton) ve alert mekanizması
        services.AddSingleton<PricingLimitsCache>();
        services.AddSingleton<IPricingLimitsProvider>(sp => sp.GetRequiredService<PricingLimitsCache>());
        services.AddSingleton<IPricingLimitsCache>(sp => sp.GetRequiredService<PricingLimitsCache>());
        services.AddSingleton<IAlertStore, MongoAlertStore>();
        services.AddSingleton<IPricingAlertChecker, PricingAlertChecker>();

        // appsettings.json dosyasındaki "MongoMarketData" ayarlarını okur
        // ve bu ayarları MongoMarketDataOptions sınıfına aktarır
        services.Configure<FixMarketDataOptions>(
            Configuration.GetSection(FixMarketDataOptions.SectionName));
        services.Configure<MongoMarketDataOptions>(
            Configuration.GetSection(MongoMarketDataOptions.SectionName));
        services.Configure<RedisOptions>(      // Redis ayarlarını okur
            Configuration.GetSection(RedisOptions.SectionName));
        services.Configure<EmailAlertOptions>(
            Configuration.GetSection(EmailAlertOptions.SectionName));

        // E-posta alert bildirimi
        services.AddSingleton<IAlertNotifier, EmailAlertNotifier>();

        // Redis bağlantısı (abortConnect: false = Redis yoksa uygulama yine de başlar)
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<RedisOptions>>().Value;
            var config = ConfigurationOptions.Parse(opts.ConnectionString);
            config.AbortOnConnectFail = false;
            config.ConnectTimeout = 3000;
            return ConnectionMultiplexer.Connect(config);
        });
        services.AddSingleton<ILatestPriceStore, RedisLatestPriceStore>();
        services.AddSingleton<IMongoLatestPriceReader, MongoLatestPriceReader>();

        // MongoClient'ı DI container'a Singleton olarak ekler
        services.AddSingleton<MongoClient>(sp =>
        {
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<MongoMarketDataOptions>>().Value;
            return new MongoClient(opts.ConnectionString);
        });

        // IMarketDataBuffer istendiğinde MongoMarketDataBuffer oluşturulur
        // Bu sınıf FIX'ten gelen verileri memory'de tutar
        // ve periyodik olarak MongoDB'ye bulk insert yapar
        services.AddSingleton<IMarketDataBuffer, MongoMarketDataBuffer>();

        // In-memory last price: FIX disconnect olsa bile API son bilinen fiyatı döner
        services.AddSingleton<IInMemoryLastPriceStore, InMemoryLastPriceStore>();

        // Observer pattern: Market data tick'leri için Subject ve Observer'lar
        services.AddSingleton<ConsoleTickObserver>();
        services.AddSingleton<MongoBufferTickObserver>();
        services.AddSingleton<RedisStoreTickObserver>();
        services.AddSingleton<InMemoryLastPriceObserver>();

        // Application katmanı: FIX parse → Domain Rules → Persistence
        services.AddSingleton<IFixMessageHandler>(sp =>
            new FixTrading.Application.Services.FixMessageHandler(
                sp.GetRequiredService<ConsoleTickObserver>(),
                sp.GetRequiredService<MongoBufferTickObserver>(),
                sp.GetRequiredService<RedisStoreTickObserver>(),
                sp.GetRequiredService<InMemoryLastPriceObserver>(),
                sp.GetRequiredService<IPricingAlertChecker>()));

        services.AddSingleton<FixApp>();
        services.AddSingleton<IFixSession, QuickFixSession>();

        services.AddScoped<InstrumentHandler>();
        services.AddScoped<LatestPriceHandler>();

        services.AddHostedService<FixListenerWorker>();
        services.AddHostedService<PricingLimitsCacheRefreshWorker>();

        // Burada uygulamanın sağlık durumunu kontrol eden Health Check'ler eklenir
        services.AddHealthChecks()
            .AddNpgSql(connectionString, name: "postgresql", tags: ["db"])   // PostgreSQL bağlantısını kontrol eder
            .AddMongoDb(sp => sp.GetRequiredService<MongoClient>(), name: "mongodb", tags: ["db"])   // MongoDB bağlantısını kontrol eder
            .AddRedis(sp =>      // Redis bağlantısını kontrol eder
            {
                var opts = sp.GetRequiredService<IOptions<RedisOptions>>().Value;
                return opts.ConnectionString;
            }, name: "redis", tags: ["cache"])
            .AddCheck<FixSessionHealthCheck>("fix_session", tags: ["fix"]);    // FIX oturumunun durumunu kontrol eder 
    }

    // Uygulama çalışırken isteklerin nasıl ilerleyeceğini belirler
    public void Configure(WebApplication app) 
    {
        var urls = Configuration["ASPNETCORE_URLS"] ?? "http://localhost:5076";   // Uygulamanın hangi URL'lerde dinleyeceğini belirler, yoksa varsayılan olarak localhost:5076 kullanır
        var baseUrl = urls.Split(';')[0].Trim();
        Console.WriteLine($"[API] Web sunucu: {baseUrl}");
        Console.WriteLine($"[API] Swagger: {baseUrl.TrimEnd('/')}/swagger");
        Console.WriteLine($"[API] Latest Price: {baseUrl.TrimEnd('/')}/api/LatestPrice");
        Console.WriteLine($"[API] Health Check: {baseUrl.TrimEnd('/')}/health");

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseAuthorization();

        // Health Check endpoint: PostgreSQL, MongoDB, Redis ve FIX oturumu durumunu döner
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = async (context, report) =>
            {
                context.Response.ContentType = "text/plain; charset=utf-8";
                var status = report.Status;    //Report: HealthCheck'lerin genel durumunu verir (Healthy, Unhealthy, Degraded)
                string message;                //Status:Sistemin genel durumu
                if (status == HealthStatus.Healthy)
                {
                    message = "Healthy";
                }
                else
                {
                    var names = report.Entries  //HealthCheck'lerin detaylarını verir, her bir check'in adı ve durumu içerir
                        .Where(e => e.Value.Status == HealthStatus.Unhealthy)
                        .Select(e => e.Key switch
                        {
                            "mongodb" => "MongoDB",
                            "redis" => "Redis",
                            "fix_session" => "FIX",
                            "postgresql" => "PostgreSQL",
                            _ => e.Key
                        });
                    message = "Unhealthy - " + string.Join(", ", names);
                }
                context.Response.StatusCode = status == HealthStatus.Healthy ? 200 : 503;
                await context.Response.WriteAsync(message);
            }
        });

        // Instrument API: HTTP eşlemesi burada, iş Handler'da (controller yok)

        // Test endpoint: Veritabanı bağlantısını ve temel işlevselliği kontrol eder
        app.MapGet("/api/Instrument/db-test", async (InstrumentHandler handler) =>  
        {
            var instruments = await handler.RetrieveAllAsync();
            return Results.Ok($"Sistem çalışıyor. Instrument sayısı: {instruments.Count}");
        });

        // CRUD endpoint'leri: En temel işlemler, tüm detaylar Handler'da (controller yok)
        app.MapGet("/api/Instrument/list", async (InstrumentHandler handler) =>
        {
            var instruments = await handler.RetrieveAllAsync();
            return Results.Ok(instruments);
        });

        // ID'ye göre retrieval: Eğer bulunamazsa 404 döner
        app.MapGet("/api/Instrument/{id:guid}", async (Guid id, InstrumentHandler handler) =>
        {
            var instrument = await handler.RetrieveByIdAsync(id);
            return instrument is null ? Results.NotFound() : Results.Ok(instrument);
        });

        // Create, Update, Delete işlemleri: Basit mesajlarla sonuç döner, detaylar Handler'da
        app.MapPost("/api/Instrument/add", async (DtoInstrument instrument, InstrumentHandler handler) =>
        {
            await handler.CreateAsync(instrument);
            return Results.Ok("Kayıt başarıyla eklendi.");
        });

        // Update işlemi: ID'ye göre güncelleme yapar, eğer ID bulunmazsa 404 döner
        app.MapPut("/api/Instrument/update/{id:guid}", async (Guid id, DtoInstrument instrument, InstrumentHandler handler) =>
        {
            await handler.UpdateAsync(id, instrument);
            return Results.Ok("Kayıt güncellendi.");
        });

        // Delete işlemi: ID'ye göre silme yapar, eğer ID bulunmazsa 404 döner
        app.MapDelete("/api/Instrument/delete/{id:guid}", async (Guid id, InstrumentHandler handler) =>
        {
            await handler.DeleteAsync(id);
            return Results.Ok("Kayıt silindi.");
        });

        // Latest Price API: HTTP eşlemesi burada, iş Handler'da (controller yok)

        // Tüm sembollerin son fiyatlarını getirir
        app.MapGet("/api/LatestPrice", async (LatestPriceHandler handler) =>
        {
            var prices = await handler.GetAllLatestAsync();
            return Results.Ok(prices);
        });

        // Belirli bir sembolün son fiyatını getirir, eğer sembol bulunmazsa 404 döner
        app.MapGet("/api/LatestPrice/{symbol}", async (string symbol, LatestPriceHandler handler) =>
        {
            var price = await handler.GetLatestAsync(symbol);
            return price is null ? Results.NotFound(new { message = $"Sembol bulunamadı: {symbol}" }) : Results.Ok(price);
        });

        // Limit simülasyonu: Veritabanındaki limitleri değiştirmeden "bu mid/spread ile alert tetiklenir mi?" testi.
        // Hiçbir yere yazmaz; sadece mevcut limitlere göre sonuç döner.
        app.MapGet("/api/Alerts/Simulate", (IPricingLimitsProvider limitsProvider, string symbol, decimal mid, decimal spread) =>
        {
            symbol = symbol.Trim().ToUpper().Replace("/", "");
            var limit = limitsProvider.GetLimit(symbol);
            if (limit == null)
                return Results.NotFound(new { wouldAlert = false, message = $"Sembol için limit tanımlı değil: {symbol}" });

            if (mid < limit.MinMid)
                return Results.Ok(new { wouldAlert = true, type = "MID_TOO_LOW", value = mid, limitValue = limit.MinMid, message = $"mid ({mid}) < min_mid ({limit.MinMid})" });
            if (mid > limit.MaxMid)
                return Results.Ok(new { wouldAlert = true, type = "MID_TOO_HIGH", value = mid, limitValue = limit.MaxMid, message = $"mid ({mid}) > max_mid ({limit.MaxMid})" });
            if (spread > limit.MaxSpread)
                return Results.Ok(new { wouldAlert = true, type = "SPREAD_LIMIT", value = spread, limitValue = limit.MaxSpread, message = $"spread ({spread}) > max_spread ({limit.MaxSpread})" });

            return Results.Ok(new { wouldAlert = false, message = "Limitler içinde", limit = new { limit.MinMid, limit.MaxMid, limit.MaxSpread } });
        });
    }
}
