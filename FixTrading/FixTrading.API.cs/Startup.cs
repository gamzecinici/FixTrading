using FixTrading.API.Controllers;
using FixTrading.API.BackgroundServices;
using FixTrading.Application;
using FixTrading.Application.Interfaces.Fix;
using FixTrading.Common.Dtos.FixSymbol;
using FixTrading.Domain.Interfaces;
using FixTrading.Infrastructure.Fix.Sessions;
using FixTrading.Persistence;
using FixTrading.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

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
        services.AddScoped<IBaseRepository<DtoFixSymbol>, FixSymbolRepository>();

        // Katman servisleri - Infrastructure (FIX)
        services.AddSingleton<FixApp>();
        services.AddSingleton<IFixSession, QuickFixSession>();

        // İç kullanım için FixSymbol handler'ı
        services.AddScoped<FixSymbolHandler>();

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
