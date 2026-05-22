using FixTrading.API.BackgroundServices;
using FixTrading.API.Controllers;
using FixTrading.API.HealthChecks;
using FixTrading.API.Hubs;
using FixTrading.API.Observers;
using FixTrading.API.cs.Pages;
using FixTrading.API.Services;
using FixTrading.Common.Dtos.Instrument;
using FixTrading.Common.Dtos.Options;
using FixTrading.Application;
using FixTrading.Application.Contracts;
using FixTrading.Application.Interfaces.Admin;
using FixTrading.Application.Interfaces.Alerts;
using FixTrading.Application.Interfaces.Fix;
using FixTrading.Application.Interfaces.MarketData;
using FixTrading.Application.Interfaces.Pricing;
using FixTrading.Application.Interfaces.Users;
using FixTrading.Domain.Interfaces;
using FixTrading.Infrastructure.Fix;
using FixTrading.Infrastructure.Fix.Sessions;
using FixTrading.Application.Interfaces;
using FixTrading.Application.Interfaces.FinancialAnalytics;
using FixTrading.Application.Services;
using FixTrading.Infrastructure.Email;
using FixTrading.Infrastructure.MongoDb;
using FixTrading.Infrastructure.Observers;
using FixTrading.Infrastructure.Pricing;
using FixTrading.Infrastructure.Redis;
using FixTrading.Infrastructure.Services;
using FixTrading.Infrastructure.Stores;
using FixTrading.Persistence;
using FixTrading.Persistence.Repositories;
using FixTrading.Persistence.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using StackExchange.Redis;

namespace FixTrading.API;

// Uygulama ilk acildiginda calisan ayar sinifi (tum kurulum burada yapilir)

public class Startup
{
    private static readonly string[] SupportedCultures = ["tr-TR", "en-US"];

    // Ayarlari (appsettings vb.) alir
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    // Servisleri sisteme tanittigimiz yer
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddRazorPages()
            .AddViewLocalization(Microsoft.AspNetCore.Mvc.Razor.LanguageViewLocationExpanderFormat.Suffix)
            .AddDataAnnotationsLocalization();

        services.AddLocalization(options => options.ResourcesPath = "Resources");
        services.AddSingleton<IStringLocalizerFactory, JsonStringLocalizerFactory>();

        // Her restart'ta yeni bir token uretilir; eski oturumlar bu token'i tasimadigindan reddedilir.
        var startupToken = new StartupTokenService();
        services.AddSingleton(startupToken);

        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath = "/Login";
                options.AccessDeniedPath = "/Login";
                options.Cookie.Name = "FixTrading.Auth";
                options.Cookie.IsEssential = true;
                options.SlidingExpiration = false;
                options.Cookie.MaxAge = null;
                // Her restart'ta olusturulan token claim'e karsi dogrulama yap.
                // Eski token tasiyan cookie'ler otomatik reddedilir => kullanici login'e yonlendirilir.
                options.Events = new CookieAuthenticationEvents
                {
                    OnValidatePrincipal = ctx =>
                    {
                        var tokenClaim = ctx.Principal?.FindFirst("app_start")?.Value;
                        if (tokenClaim != startupToken.Token)
                            ctx.RejectPrincipal();
                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("AdminOnly", policy => policy.RequireRole("admin"));
        });
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
        services.AddScoped<IUserAccountService, UserAccountService>();
        services.AddScoped<IPricingLimitsQueryService, PricingLimitsQueryService>();
        services.AddScoped<IPricingLimitMutationService, PricingLimitMutationService>();
        services.AddScoped<IInstrumentSymbolAdminService, InstrumentSymbolAdminService>();
        services.AddSingleton<ISystemParameterService, SystemParameterService>();
        services.Configure<AIOptions>(Configuration.GetSection(AIOptions.SectionName));
        services.AddSingleton<IAIFinanceAssistantService, GroqFinanceAssistantService>();

        // Pricing limits cache (singleton) ve alert mekanizmasi
        services.AddSingleton<PricingLimitsCache>();
        services.AddSingleton<IPricingLimitsProvider>(sp => sp.GetRequiredService<PricingLimitsCache>());
        services.AddSingleton<IPricingLimitsCache>(sp => sp.GetRequiredService<PricingLimitsCache>());
        services.AddSingleton<IAlertStore, MongoAlertStore>();
        services.AddSingleton<IPricingAlertChecker, PricingAlertChecker>();

        // appsettings.json dosyasindaki "MongoMarketData" ayarlarini okur
        // ve bu ayarlari MongoMarketDataOptions sinifina aktarir
        services.Configure<FixMarketDataOptions>(
            Configuration.GetSection(FixMarketDataOptions.SectionName));
        services.Configure<MongoMarketDataOptions>(
            Configuration.GetSection(MongoMarketDataOptions.SectionName));
        services.Configure<RedisOptions>(      // Redis ayarlarini okur
            Configuration.GetSection(RedisOptions.SectionName));
        services.Configure<EmailAlertOptions>(
            Configuration.GetSection(EmailAlertOptions.SectionName));

        // E-posta alert bildirimi
        services.AddSingleton<IAlertNotifier, EmailAlertNotifier>();

        // Redis baglantisi (abortConnect: false = Redis yoksa uygulama yine de baslar)
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<RedisOptions>>().Value;
            var config = ConfigurationOptions.Parse(opts.ConnectionString);
            config.AbortOnConnectFail = false;
            config.ConnectTimeout = 2000; // Timeout'u biraz daha dusurelim
            config.ConnectRetry = 1;      // Cok fazla tekrar denemesin
            return ConnectionMultiplexer.Connect(config);
        });
        services.AddSingleton<ILatestPriceStore, RedisLatestPriceStore>();
        services.AddSingleton<IMongoLatestPriceReader, MongoLatestPriceReader>();

        // MongoClient'i DI container'a Singleton olarak ekler
        services.AddSingleton<MongoClient>(sp =>
        {
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<MongoMarketDataOptions>>().Value;
            return new MongoClient(opts.ConnectionString);
        });

        // IMarketDataBuffer istendiginde MongoMarketDataBuffer olusturulur
        // Bu sinif FIX'ten gelen verileri memory'de tutar
        // ve periyodik olarak MongoDB'ye bulk insert yapar
        services.AddSingleton<IMarketDataBuffer, MongoMarketDataBuffer>();

        // In-memory last price: FIX disconnect olsa bile API son bilinen fiyati doner
        services.AddSingleton<IInMemoryLastPriceStore, InMemoryLastPriceStore>();

        // Observer pattern: Market data tick'leri icin Subject ve Observer'lar
        services.AddSingleton<ConsoleTickObserver>();
        services.AddSingleton<MongoBufferTickObserver>();
        services.AddSingleton<RedisStoreTickObserver>();
        services.AddSingleton<InMemoryLastPriceObserver>();

        // Application katmani: FIX parse => Domain Rules => Persistence
        services.AddSingleton<IFixMessageHandler>(sp =>
            new FixTrading.Application.Services.FixMessageHandler(
                sp.GetRequiredService<ConsoleTickObserver>(),
                sp.GetRequiredService<MongoBufferTickObserver>(),
                sp.GetRequiredService<RedisStoreTickObserver>(),
                sp.GetRequiredService<InMemoryLastPriceObserver>(),
                sp.GetRequiredService<IPricingAlertChecker>(),
                sp.GetRequiredService<SignalRTickObserver>()));

        services.AddSingleton<FixApp>();
        services.AddSingleton<IFixSession, QuickFixSession>();

        services.AddSignalR();
        services.AddSingleton<IMarketHubService, MarketHubService>();
        services.AddSingleton<SignalRTickObserver>();

        services.AddScoped<InstrumentHandler>();
        services.AddSingleton<LatestPriceHandler>();
        services.AddSingleton<FinancialAnalyticsMongoSource>();
        services.AddSingleton<FinancialAnalyticsCalculators>();
        services.AddSingleton<FinancialAnalyticsService>();
        services.AddScoped<AdminPageServices>();
        services.AddScoped<AdminPageOperations>();
        services.AddScoped<AdminPageMongoCollections>();
        services.AddScoped<UserPageServices>();
        services.AddScoped<UserPageMongoCollections>();

        services.AddHostedService<FixListenerWorker>();
        services.AddHostedService<PricingLimitsCacheRefreshWorker>();

        // Burada uygulamanin saglik durumunu kontrol eden Health Check'ler eklenir
        services.AddHealthChecks()
            .AddNpgSql(connectionString, name: "postgresql", tags: ["db"])   // PostgreSQL baglantisini kontrol eder
            .AddMongoDb(sp => sp.GetRequiredService<MongoClient>(), name: "mongodb", tags: ["db"])   // MongoDB baglantisini kontrol eder
            .AddRedis(sp =>      // Redis baglantisini kontrol eder
            {
                var opts = sp.GetRequiredService<IOptions<RedisOptions>>().Value;
                return opts.ConnectionString;
            }, name: "redis", tags: ["cache"])
            .AddCheck<FixSessionHealthCheck>("fix_session", tags: ["fix"]);    // FIX oturumunun durumunu kontrol eder 
    }

    // Uygulama calisirken isteklerin nasil ilerleyecegini belirler
    public void Configure(WebApplication app) 
    {
        var urls = Configuration["ASPNETCORE_URLS"] ?? "http://localhost:5076";   // Uygulamanin hangi URL'lerde dinleyecegini belirler, yoksa varsayilan olarak localhost:5076 kullanir
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

        app.UseStaticFiles();

        var localizationOptions = new RequestLocalizationOptions()
            .SetDefaultCulture(SupportedCultures[0])
            .AddSupportedCultures(SupportedCultures)
            .AddSupportedUICultures(SupportedCultures);

        // Tarayıcının Accept-Language başlığının uygulamanın kendi dil seçimini (cookie) ezmesini engelle.
        // Yalnızca QueryString ve Cookie provider'ları aktif; varsayılan dil tr-TR.
        localizationOptions.RequestCultureProviders = localizationOptions.RequestCultureProviders
            .Where(p => p is not Microsoft.AspNetCore.Localization.AcceptLanguageHeaderRequestCultureProvider)
            .ToList();

        app.UseRequestLocalization(localizationOptions);

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapGet("/", () => Results.Redirect("/Login"));
        app.MapRazorPages();
        app.MapHub<MarketHub>("/hubs/market");

        app.MapGet("/SetLanguage", SetLanguage);

        var adminApi = app.MapGroup("/api/admin").RequireAuthorization("AdminOnly");
        adminApi.MapGet("/symbols", async (IInstrumentSymbolAdminService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListAsync(ct)));
        adminApi.MapPost("/symbols", AddSymbolAsync);
        adminApi.MapDelete("/symbols/{instrumentId:guid}", async (Guid instrumentId, IInstrumentSymbolAdminService svc, CancellationToken ct) =>
        {
            var (ok, error) = await svc.DeleteAsync(instrumentId, ct);
            return ok ? Results.Ok(new { ok = true }) : Results.NotFound(new { error });
        });

        // ── Önizleme: Enstrüman ekleme ekranında girilen sembol için anlık FIX fiyatı ─────────────
        // Sembol zaten cache'te varsa (Redis/Mongo/InMemory) direkt döner.
        // Aksi halde FIX'e abone olur ve ilk snapshot için kısa süre bekler.
        // Broker reddederse veya zaman aşımı olursa null status döner; UI buna göre mesaj gösterir.
        adminApi.MapGet("/symbols/preview/{symbol}", PreviewSymbolAsync);

        adminApi.MapPost("/test-email", TestEmailAsync);

        // Health Check endpoint: PostgreSQL, MongoDB, Redis ve FIX oturumu durumunu doner
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = WriteHealthResponseAsync
        });

        // Instrument API: HTTP eslemesi burada, is Handler'da (controller yok)

        // Test endpoint: Veritabani baglantisini ve temel islevselligi kontrol eder
        app.MapGet("/api/Instrument/db-test", async (InstrumentHandler handler) =>  
        {
            var instruments = await handler.RetrieveAllAsync();
            return Results.Ok($"Sistem calisiyor. Instrument sayisi: {instruments.Count}");
        });

        // CRUD endpoint'leri: En temel islemler, tum detaylar Handler'da (controller yok)
        app.MapGet("/api/Instrument/list", async (InstrumentHandler handler) =>
        {
            var instruments = await handler.RetrieveAllAsync();
            return Results.Ok(instruments);
        });

        // ID'ye gore retrieval: Eger bulunamazsa 404 doner
        app.MapGet("/api/Instrument/{id:guid}", GetInstrumentByIdAsync);

        // Create, Update, Delete islemleri: Basit mesajlarla sonuc doner, detaylar Handler'da
        app.MapPost("/api/Instrument/add", async (DtoInstrument instrument, InstrumentHandler handler) =>
        {
            await handler.CreateAsync(instrument);
            return Results.Ok("Kayit basariyla eklendi.");
        });

        // Update islemi: ID'ye gore guncelleme yapar, eger ID bulunmazsa 404 doner
        app.MapPut("/api/Instrument/update/{id:guid}", async (Guid id, DtoInstrument instrument, InstrumentHandler handler) =>
        {
            await handler.UpdateAsync(id, instrument);
            return Results.Ok("Kayit guncellendi.");
        });

        // Delete islemi: ID'ye gore silme yapar, eger ID bulunmazsa 404 doner
        app.MapDelete("/api/Instrument/delete/{id:guid}", async (Guid id, InstrumentHandler handler) =>
        {
            await handler.DeleteAsync(id);
            return Results.Ok("Kayit silindi.");
        });

        // Latest Price API: HTTP eslemesi burada, is Handler'da (controller yok)

        // Tum sembollerin son fiyatlarini getirir
        app.MapGet("/api/LatestPrice", async (LatestPriceHandler handler) =>
        {
            var prices = await handler.GetAllLatestAsync();
            return Results.Ok(prices);
        });

        // Belirli bir sembolun son fiyatini getirir, eger sembol bulunmazsa 404 doner
        app.MapGet("/api/LatestPrice/{symbol}", GetLatestPriceBySymbolAsync);

        // Limit simulasyonu: Veritabanindaki limitleri degistirmeden "bu mid/spread ile alert tetiklenir mi?" testi.
        // Hicbir yere yazmaz; sadece mevcut limitlere gore sonuc doner.
        app.MapGet("/api/Alerts/Simulate", SimulateAlert);
    }

    private static IResult SetLanguage(string? culture, string? returnUrl, HttpContext context)
    {
        culture = NormalizeCulture(culture);
        var cookieName = Microsoft.AspNetCore.Localization.CookieRequestCultureProvider.DefaultCookieName;

        context.Response.Cookies.Delete(cookieName, new CookieOptions { Path = "/" });
        context.Response.Cookies.Append(
            cookieName,
            Microsoft.AspNetCore.Localization.CookieRequestCultureProvider.MakeCookieValue(
                new Microsoft.AspNetCore.Localization.RequestCulture(culture)),
            BuildLanguageCookieOptions(context));

        Console.WriteLine($"[Localization] SetLanguage -> culture={culture}, returnUrl={returnUrl}");

        context.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        context.Response.Headers.Pragma = "no-cache";

        return Results.LocalRedirect(NormalizeLocalRedirect(returnUrl));
    }

    private static string NormalizeCulture(string? culture)
        => !string.IsNullOrWhiteSpace(culture) && SupportedCultures.Contains(culture) ? culture : "tr-TR";

    private static CookieOptions BuildLanguageCookieOptions(HttpContext context)
        => new()
        {
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            Path = "/",
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
            HttpOnly = true,
            Secure = context.Request.IsHttps
        };

    private static string NormalizeLocalRedirect(string? returnUrl)
    {
        var redirectTo = returnUrl ?? "/";
        return Uri.IsWellFormedUriString(redirectTo, UriKind.Relative) ? redirectTo : "/";
    }

    private static async Task<IResult> AddSymbolAsync(
        AddSymbolApiRequest body,
        IInstrumentSymbolAdminService svc,
        HttpContext ctx,
        CancellationToken ct)
    {
        if (body is null || string.IsNullOrWhiteSpace(body.Symbol))
            return Results.BadRequest(new { error = "Sembol gerekli." });

        var auditUser = CurrentUserAudit.GetDisplayNameForAudit(ctx.User);
        if (string.IsNullOrEmpty(auditUser))
            return Results.Unauthorized();

        var (ok, error, created) = await svc.AddAsync(body, auditUser, ct);

        return ok
            ? Results.Ok(new { ok = true, item = created })
            : Results.BadRequest(new { error });
    }

    private static async Task<IResult> PreviewSymbolAsync(
        string symbol,
        IFixSession fixSession,
        LatestPriceHandler priceHandler,
        IStringLocalizer<SharedResource> localizer,
        CancellationToken ct)
    {
        var validationError = ValidatePreviewSymbol(symbol, localizer, out var normalized);
        if (validationError is not null)
            return validationError;

        var cached = await priceHandler.GetLatestAsync(normalized);
        if (cached is not null)
            return Results.Ok(new { status = "ok", data = cached, subscribed = fixSession.IsSubscribed(normalized) });

        if (!fixSession.IsConnected)
            return Results.Ok(new { status = "fix_disconnected", message = localizer["FixDisconnectedDataError"].Value });

        var subscribeError = TrySubscribeForPreview(normalized, fixSession, localizer);
        if (subscribeError is not null)
            return subscribeError;

        return await WaitForPreviewPriceAsync(normalized, fixSession, priceHandler, localizer, ct);
    }

    private static IResult? ValidatePreviewSymbol(
        string symbol,
        IStringLocalizer<SharedResource> localizer,
        out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(symbol))
            return Results.BadRequest(new { error = localizer["SymbolRequired"].Value });

        normalized = symbol.Trim().ToUpperInvariant().Replace("/", string.Empty).Replace(" ", string.Empty);
        return normalized.Length < 2 || normalized.Length > 20
            ? Results.BadRequest(new { error = localizer["InvalidSymbolLength"].Value })
            : null;
    }

    private static IResult? TrySubscribeForPreview(
        string normalized,
        IFixSession fixSession,
        IStringLocalizer<SharedResource> localizer)
    {
        if (fixSession.IsSubscribed(normalized))
            return null;

        try
        {
            fixSession.Subscribe(normalized);
            return null;
        }
        catch (Exception ex)
        {
            return Results.Ok(new { status = "error", message = string.Format(localizer["SubscribeError"].Value, ex.Message) });
        }
    }

    private static async Task<IResult> WaitForPreviewPriceAsync(
        string normalized,
        IFixSession fixSession,
        LatestPriceHandler priceHandler,
        IStringLocalizer<SharedResource> localizer,
        CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            await Task.Delay(150, ct);
            var data = await priceHandler.GetLatestAsync(normalized);
            if (data is not null)
                return Results.Ok(new { status = "ok", data, subscribed = true });

            if (!fixSession.IsSubscribed(normalized))
                return Results.Ok(new { status = "rejected", message = localizer["BrokerSubscriptionRejected"].Value });
        }

        return Results.Ok(new { status = "pending", message = localizer["NoDataYetFixPending"].Value, subscribed = fixSession.IsSubscribed(normalized) });
    }

    private static async Task<IResult> TestEmailAsync(
        IOptionsMonitor<EmailAlertOptions> monitor,
        CancellationToken ct)
    {
        var opts = monitor.CurrentValue;
        if (!opts.Enabled)
            return Results.BadRequest(new { error = "EmailAlert disabled (Enabled: false)." });

        try
        {
            using var client = new MailKit.Net.Smtp.SmtpClient();
            var ssl = opts.UseSsl ? MailKit.Security.SecureSocketOptions.StartTls : MailKit.Security.SecureSocketOptions.None;
            await client.ConnectAsync(opts.SmtpHost, opts.SmtpPort, ssl, ct);
            var password = (opts.Password ?? string.Empty).Trim().Replace(" ", string.Empty);
            await client.AuthenticateAsync(opts.Username.Trim(), password, ct);
            await client.DisconnectAsync(true, ct);
            return Results.Ok(new { ok = true, message = "SMTP connection and authentication successful." });
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task WriteHealthResponseAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "text/plain; charset=utf-8";
        var status = report.Status;
        context.Response.StatusCode = status == HealthStatus.Healthy ? 200 : 503;
        await context.Response.WriteAsync(BuildHealthMessage(report));
    }

    private static string BuildHealthMessage(HealthReport report)
    {
        if (report.Status == HealthStatus.Healthy)
            return "Healthy";

        var names = report.Entries
            .Where(e => e.Value.Status == HealthStatus.Unhealthy)
            .Select(e => e.Key switch
            {
                "mongodb" => "MongoDB",
                "redis" => "Redis",
                "fix_session" => "FIX",
                "postgresql" => "PostgreSQL",
                _ => e.Key
            });
        return "Unhealthy - " + string.Join(", ", names);
    }

    private static async Task<IResult> GetInstrumentByIdAsync(Guid id, InstrumentHandler handler)
    {
        var instrument = await handler.RetrieveByIdAsync(id);
        return instrument is null ? Results.NotFound() : Results.Ok(instrument);
    }

    private static async Task<IResult> GetLatestPriceBySymbolAsync(string symbol, LatestPriceHandler handler)
    {
        var price = await handler.GetLatestAsync(symbol);
        return price is null ? Results.NotFound(new { message = $"Sembol bulunamadi: {symbol}" }) : Results.Ok(price);
    }

    private static IResult SimulateAlert(IPricingLimitsProvider limitsProvider, string symbol, decimal mid, decimal spread)
    {
        symbol = symbol.Trim().ToUpper().Replace("/", "");
        var limit = limitsProvider.GetLimit(symbol);
        if (limit == null)
            return Results.NotFound(new { wouldAlert = false, message = $"Sembol icin limit tanimli degil: {symbol}" });

        if (mid < limit.MinMid)
            return Results.Ok(new { wouldAlert = true, type = "MID_TOO_LOW", value = mid, limitValue = limit.MinMid, message = $"mid ({mid}) < min_mid ({limit.MinMid})" });
        if (mid > limit.MaxMid)
            return Results.Ok(new { wouldAlert = true, type = "MID_TOO_HIGH", value = mid, limitValue = limit.MaxMid, message = $"mid ({mid}) > max_mid ({limit.MaxMid})" });
        if (spread > limit.MaxSpread)
            return Results.Ok(new { wouldAlert = true, type = "SPREAD_LIMIT", value = spread, limitValue = limit.MaxSpread, message = $"spread ({spread}) > max_spread ({limit.MaxSpread})" });

        return Results.Ok(new { wouldAlert = false, message = "Limitler icinde", limit = new { limit.MinMid, limit.MaxMid, limit.MaxSpread } });
    }
}
