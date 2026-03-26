using FixTrading.API.Controllers;
using FixTrading.Common.Dtos.Alert;
using FixTrading.Common.Dtos.MarketData;
using FixTrading.Infrastructure.MongoDb;
using FixTrading.Persistence;
using FixTrading.Persistence.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace FixTrading.API.cs.Pages;

[Authorize(Roles = "admin")]
public class AdminModel : PageModel
{
    private readonly AppDbContext _dbContext;
    private readonly LatestPriceHandler _latestPriceHandler;
    private readonly HealthCheckService _healthCheckService;
    private readonly IMongoCollection<DtoAlert> _alertsCollection;

    public AdminModel(
        AppDbContext dbContext,
        LatestPriceHandler latestPriceHandler,
        HealthCheckService healthCheckService,
        MongoClient mongoClient,
        IOptions<MongoMarketDataOptions> mongoOptions)
    {
        _dbContext = dbContext;
        _latestPriceHandler = latestPriceHandler;
        _healthCheckService = healthCheckService;
        var database = mongoClient.GetDatabase(mongoOptions.Value.DatabaseName);
        _alertsCollection = database.GetCollection<DtoAlert>(MongoAlertStore.AlertsCollectionName);
    }

    public string ActiveTab { get; private set; } = "home";
    public List<ServiceHealthVm> HealthServices { get; private set; } = [];
    public List<DtoMarketData> MarketRows { get; private set; } = [];
    public List<UserEntity> Users { get; private set; } = [];
    public List<PricingLimitRowVm> PricingLimits { get; private set; } = [];
    public List<DtoAlert> Alerts { get; private set; } = [];

    [BindProperty]
    public AddUserInput NewUser { get; set; } = new();

    public async Task OnGetAsync(string? tab = null)
    {
        ActiveTab = NormalizeTab(tab);
        await LoadAllAsync();
    }

    public async Task<IActionResult> OnGetLiveMarketAsync()
    {
        var market = await _latestPriceHandler.GetAllLatestAsync();
        var rows = market
            .OrderBy(x => x.Symbol)
            .Select(x => new
            {
                x.Symbol,
                x.Bid,
                x.Ask,
                x.Mid,
                x.Spread
            })
            .ToList();
        return new JsonResult(rows);
    }

    public async Task<IActionResult> OnPostAddUserAsync(string? tab = null)
    {
        if (string.IsNullOrWhiteSpace(NewUser.FullName) ||
            string.IsNullOrWhiteSpace(NewUser.Email) ||
            string.IsNullOrWhiteSpace(NewUser.Password))
        {
            return RedirectToPage(new { tab = "users" });
        }

        var normalizedEmail = NewUser.Email.Trim().ToLowerInvariant();
        var exists = await _dbContext.Users.AnyAsync(u => u.Email.ToLower() == normalizedEmail);
        if (exists)
        {
            return RedirectToPage(new { tab = "users" });
        }

        var entity = new UserEntity
        {
            FullName = NewUser.FullName.Trim(),
            Email = normalizedEmail,
            Password = BCrypt.Net.BCrypt.HashPassword(NewUser.Password),
            Role = "user"
        };

        _dbContext.Users.Add(entity);
        await _dbContext.SaveChangesAsync();
        return RedirectToPage(new { tab = "users" });
    }

    public async Task<IActionResult> OnPostDeleteUserAsync(int id)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is not null)
        {
            _dbContext.Users.Remove(user);
            await _dbContext.SaveChangesAsync();
        }

        return RedirectToPage(new { tab = "users" });
    }

    public async Task<IActionResult> OnPostToggleUserRoleAsync(int id)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is not null)
        {
            user.Role = string.Equals(user.Role, "admin", StringComparison.OrdinalIgnoreCase) ? "user" : "admin";
            await _dbContext.SaveChangesAsync();
        }

        return RedirectToPage(new { tab = "users" });
    }

    public async Task<IActionResult> OnPostUpdateLimitAsync(Guid id, decimal minMid, decimal maxMid, decimal maxSpread)
    {
        var limit = await _dbContext.PricingLimits.FirstOrDefaultAsync(x => x.Id == id);
        if (limit is not null)
        {
            limit.MinMid = minMid;
            limit.MaxMid = maxMid;
            limit.MaxSpread = maxSpread;
            await _dbContext.SaveChangesAsync();
        }

        return RedirectToPage(new { tab = "limits" });
    }

    public async Task<IActionResult> OnPostDeleteLimitAsync(Guid id)
    {
        var limit = await _dbContext.PricingLimits.FirstOrDefaultAsync(x => x.Id == id);
        if (limit is not null)
        {
            _dbContext.PricingLimits.Remove(limit);
            await _dbContext.SaveChangesAsync();
        }

        return RedirectToPage(new { tab = "limits" });
    }

    private async Task LoadAllAsync()
    {
        await LoadHealthAsync();
        MarketRows = await _latestPriceHandler.GetAllLatestAsync();
        Users = await _dbContext.Users
            .AsNoTracking()
            .OrderBy(x => x.FullName)
            .ToListAsync();

        PricingLimits = await _dbContext.PricingLimits
            .AsNoTracking()
            .Include(x => x.Instrument)
            .OrderBy(x => x.Instrument != null ? x.Instrument.Symbol : string.Empty)
            .Select(x => new PricingLimitRowVm
            {
                Id = x.Id,
                Symbol = x.Instrument != null ? x.Instrument.Symbol : "-",
                MinMid = x.MinMid,
                MaxMid = x.MaxMid,
                MaxSpread = x.MaxSpread
            })
            .ToListAsync();

        Alerts = await _alertsCollection
            .Find(Builders<DtoAlert>.Filter.Empty)
            .SortByDescending(x => x.Time)
            .Limit(150)
            .ToListAsync();
    }

    private async Task LoadHealthAsync()
    {
        var report = await _healthCheckService.CheckHealthAsync();
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["redis"] = "Redis",
            ["mongodb"] = "MongoDB",
            ["fix_session"] = "FIX Connection",
            ["postgresql"] = "PostgreSQL"
        };

        var order = new[] { "redis", "mongodb", "fix_session", "postgresql" };
        HealthServices = order
            .Select(key =>
            {
                var exists = report.Entries.TryGetValue(key, out var entry);
                var healthy = exists && entry.Status == HealthStatus.Healthy;
                return new ServiceHealthVm
                {
                    Name = map[key],
                    IsHealthy = healthy
                };
            })
            .ToList();
    }

    private static string NormalizeTab(string? tab)
    {
        var allowed = new[] { "home", "market", "users", "limits", "alerts" };
        return allowed.Contains(tab ?? "", StringComparer.OrdinalIgnoreCase)
            ? tab!.ToLowerInvariant()
            : "home";
    }
}

public class ServiceHealthVm
{
    public string Name { get; set; } = string.Empty;
    public bool IsHealthy { get; set; }
}

public class PricingLimitRowVm
{
    public Guid Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public decimal MinMid { get; set; }
    public decimal MaxMid { get; set; }
    public decimal MaxSpread { get; set; }
}

public class AddUserInput
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Role { get; set; } = "user";
}

