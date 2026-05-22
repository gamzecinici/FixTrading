using FixTrading.Application.Interfaces.Arbitrage;
using FixTrading.Application.Interfaces.FinancialAnalytics;
using FixTrading.Common.Dtos.MarketData;
using FixTrading.Common.Dtos.Options;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace FixTrading.API.Services;

public sealed class FinancialAnalyticsMongoSource
{
    public FinancialAnalyticsMongoSource(MongoClient mongoClient, IOptions<MongoMarketDataOptions> mongoOptions)
    {
        var database = mongoClient.GetDatabase(mongoOptions.Value.DatabaseName);
        Ticks = database.GetCollection<DtoMarketData>(mongoOptions.Value.CollectionName);
    }

    public IMongoCollection<DtoMarketData> Ticks { get; }
}

public sealed class FinancialAnalyticsCalculators
{
    public FinancialAnalyticsCalculators(
        IArbitrageService arbitrage,
        IVolatilityAnalyticsService volatility,
        IRiskAnalyticsService risk,
        IAIFinanceAssistantService aiAssistant)
    {
        Arbitrage = arbitrage;
        Volatility = volatility;
        Risk = risk;
        AiAssistant = aiAssistant;
    }

    public IArbitrageService Arbitrage { get; }
    public IVolatilityAnalyticsService Volatility { get; }
    public IRiskAnalyticsService Risk { get; }
    public IAIFinanceAssistantService AiAssistant { get; }
}
