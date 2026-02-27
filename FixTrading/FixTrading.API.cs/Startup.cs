using FixTrading.API.Controllers;
using FixTrading.API.BackgroundServices;
using FixTrading.Application;
using FixTrading.Application.Interfaces.Fix;
using FixTrading.Application.Interfaces.MarketData;
using FixTrading.Domain.Interfaces;
using FixTrading.Infrastructure.Fix;
using FixTrading.Infrastructure.Fix.Sessions;
using FixTrading.Infrastructure.MongoDb;
using FixTrading.Persistence;
using FixTrading.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;

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
        // HTTP API controller'ları
        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        // Katman servisleri - Application
        services.AddApplicationServices();

        // Katman servisleri - Persistence (DB)
        var connectionString = Configuration.GetConnectionString("DefaultConnection")!;
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));
        services.AddScoped<IInstrumentRepository, InstrumentRepository>();
        services.AddScoped<ITradeRepository, TradeRepository>();

        // appsettings.json dosyasındaki "MongoMarketData" ayarlarını okur
        // ve bu ayarları MongoMarketDataOptions sınıfına aktarır
        services.Configure<FixMarketDataOptions>(
            Configuration.GetSection(FixMarketDataOptions.SectionName));
        services.Configure<MongoMarketDataOptions>(
            Configuration.GetSection(MongoMarketDataOptions.SectionName));

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
        
        services.AddSingleton<FixApp>();
        services.AddSingleton<IFixSession, QuickFixSession>();

        // İç kullanım için Instrument handler'ı
        services.AddScoped<InstrumentHandler>();

        // Arka plan FIX dinleyici servisi
        services.AddHostedService<FixListenerWorker>();
    }

    // Uygulama çalışırken isteklerin nasıl ilerleyeceğini belirler
    public void Configure(WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseAuthorization();
        app.MapControllers();
    }
}
