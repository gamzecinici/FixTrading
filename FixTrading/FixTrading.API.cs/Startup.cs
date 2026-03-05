using FixTrading.API.Controllers;
using FixTrading.API.BackgroundServices;
using FixTrading.Common.Dtos.Instrument;
using FixTrading.Application;
using FixTrading.Application.Interfaces.Fix;
using FixTrading.Application.Interfaces.MarketData;
using FixTrading.Domain.Interfaces;
using FixTrading.Infrastructure.Fix;
using FixTrading.Infrastructure.Fix.Sessions;
using FixTrading.Infrastructure.MongoDb;
using FixTrading.Infrastructure.Observers;
using FixTrading.Infrastructure.Redis;
using FixTrading.Persistence;
using FixTrading.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
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

        // appsettings.json dosyasındaki "MongoMarketData" ayarlarını okur
        // ve bu ayarları MongoMarketDataOptions sınıfına aktarır
        services.Configure<FixMarketDataOptions>(
            Configuration.GetSection(FixMarketDataOptions.SectionName));
        services.Configure<MongoMarketDataOptions>(
            Configuration.GetSection(MongoMarketDataOptions.SectionName));
        services.Configure<RedisOptions>(      // Redis ayarlarını okur
            Configuration.GetSection(RedisOptions.SectionName));

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

        // Observer pattern: Market data tick'leri için Subject ve Observer'lar
        services.AddSingleton<ConsoleTickObserver>();
        services.AddSingleton<MongoBufferTickObserver>();
        services.AddSingleton<RedisStoreTickObserver>();
        services.AddSingleton<IMarketDataSubject>(sp =>
        {
            var subject = new MarketDataSubject();
            subject.Attach(sp.GetRequiredService<ConsoleTickObserver>());
            subject.Attach(sp.GetRequiredService<MongoBufferTickObserver>());
            subject.Attach(sp.GetRequiredService<RedisStoreTickObserver>());
            return subject;
        });

        services.AddSingleton<FixApp>();
        services.AddSingleton<IFixSession, QuickFixSession>();

        services.AddScoped<InstrumentHandler>();
        services.AddScoped<LatestPriceHandler>();

        // Arka plan FIX dinleyici servisi
        services.AddHostedService<FixListenerWorker>();
    }

    // Uygulama çalışırken isteklerin nasıl ilerleyeceğini belirler
    public void Configure(WebApplication app)
    {
        var urls = Configuration["ASPNETCORE_URLS"] ?? "http://localhost:5076";
        var baseUrl = urls.Split(';')[0].Trim();
        Console.WriteLine($"[API] Web sunucu: {baseUrl}");
        Console.WriteLine($"[API] Swagger: {baseUrl.TrimEnd('/')}/swagger");
        Console.WriteLine($"[API] Latest Price: {baseUrl.TrimEnd('/')}/api/LatestPrice");

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseAuthorization();

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
    }
}
