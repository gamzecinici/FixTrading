using FixTrading.Common.Dtos.FinancialAnalytics;
using FixTrading.Common.Dtos.MarketData;

namespace FixTrading.Application.Interfaces.FinancialAnalytics;

// Sadece hesaplama yapar: son fiyat, limit ve volatilite metriklerinden risk satiri uretir.
public interface IRiskAnalyticsService
{
    FinancialRiskRowDto BuildRow(
        string symbol,
        DtoMarketData? latest,
        FinancialActiveLimitRow? lim,
        VolatilityMetrics volatility,
        Dictionary<string, string> cfg);
}
