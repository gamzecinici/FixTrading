using FixTrading.API.Controllers;
using FixTrading.API.Services;
using FixTrading.Application.Interfaces;
using FixTrading.Application.Interfaces.Arbitrage;
using FixTrading.Application.Interfaces.Instrument;
using FixTrading.Application.Interfaces.Pricing;
using FixTrading.Application.Interfaces.Users;
using FixTrading.Common.Dtos.Alert;
using FixTrading.Common.Dtos.MarketData;
using FixTrading.Common.Dtos.Options;
using FixTrading.Infrastructure.MongoDb;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace FixTrading.API.cs.Pages;

public sealed class AdminPageServices
{
    public AdminPageServices(
        LatestPriceHandler latestPriceHandler,
        HealthCheckService healthCheckService,
        FinancialAnalyticsService financialAnalytics,
        IInstrumentService instrumentService,
        IArbitrageService arbitrageService)
    {
        LatestPriceHandler = latestPriceHandler;
        HealthCheckService = healthCheckService;
        FinancialAnalytics = financialAnalytics;
        InstrumentService = instrumentService;
        ArbitrageService = arbitrageService;
    }

    public LatestPriceHandler LatestPriceHandler { get; }
    public HealthCheckService HealthCheckService { get; }
    public FinancialAnalyticsService FinancialAnalytics { get; }
    public IInstrumentService InstrumentService { get; }
    public IArbitrageService ArbitrageService { get; }
}

public sealed class AdminPageOperations
{
    public AdminPageOperations(
        IPricingLimitsSyncService pricingLimitsSync,
        IPricingLimitsQueryService pricingLimitsQuery,
        IPricingLimitMutationService pricingLimitMutation,
        IUserAccountService userAccountService,
        ISystemParameterService systemParameterService)
    {
        PricingLimitsSync = pricingLimitsSync;
        PricingLimitsQuery = pricingLimitsQuery;
        PricingLimitMutation = pricingLimitMutation;
        UserAccountService = userAccountService;
        SystemParameterService = systemParameterService;
    }

    public IPricingLimitsSyncService PricingLimitsSync { get; }
    public IPricingLimitsQueryService PricingLimitsQuery { get; }
    public IPricingLimitMutationService PricingLimitMutation { get; }
    public IUserAccountService UserAccountService { get; }
    public ISystemParameterService SystemParameterService { get; }
}

public sealed class AdminPageMongoCollections
{
    public AdminPageMongoCollections(MongoClient mongoClient, IOptions<MongoMarketDataOptions> mongoOptions)
    {
        var database = mongoClient.GetDatabase(mongoOptions.Value.DatabaseName);
        Alerts = database.GetCollection<DtoAlert>(MongoAlertStore.AlertsCollectionName);
        MarketData = database.GetCollection<DtoMarketData>(mongoOptions.Value.CollectionName);
    }

    public IMongoCollection<DtoAlert> Alerts { get; }
    public IMongoCollection<DtoMarketData> MarketData { get; }
}

public sealed class UserPageServices
{
    public UserPageServices(
        IPricingLimitsQueryService pricingLimitsQuery,
        LatestPriceHandler latestPriceHandler,
        FinancialAnalyticsService financialAnalytics,
        ISystemParameterService sysParam,
        IInstrumentService instrumentService,
        IArbitrageService arbitrageService)
    {
        PricingLimitsQuery = pricingLimitsQuery;
        LatestPriceHandler = latestPriceHandler;
        FinancialAnalytics = financialAnalytics;
        SysParam = sysParam;
        InstrumentService = instrumentService;
        ArbitrageService = arbitrageService;
    }

    public IPricingLimitsQueryService PricingLimitsQuery { get; }
    public LatestPriceHandler LatestPriceHandler { get; }
    public FinancialAnalyticsService FinancialAnalytics { get; }
    public ISystemParameterService SysParam { get; }
    public IInstrumentService InstrumentService { get; }
    public IArbitrageService ArbitrageService { get; }
}

public sealed class UserPageMongoCollections
{
    public UserPageMongoCollections(MongoClient mongoClient, IOptions<MongoMarketDataOptions> mongoOptions)
    {
        var database = mongoClient.GetDatabase(mongoOptions.Value.DatabaseName);
        MarketData = database.GetCollection<DtoMarketData>(mongoOptions.Value.CollectionName);
    }

    public IMongoCollection<DtoMarketData> MarketData { get; }
}
