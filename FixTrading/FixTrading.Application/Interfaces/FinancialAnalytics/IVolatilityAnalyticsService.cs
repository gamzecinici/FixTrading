using FixTrading.Common.Dtos.FinancialAnalytics;

namespace FixTrading.Application.Interfaces.FinancialAnalytics;

// Sadece hesaplama yapar: fiyat serisinden volatilite metrikleri ve UI satiri uretir.
public interface IVolatilityAnalyticsService
{
    VolatilityMetrics ComputeMetrics(IReadOnlyList<decimal> mids, Dictionary<string, string> cfg);

    FinancialVolatilityRowDto BuildRow(string symbol, VolatilityMetrics metrics, Dictionary<string, string> cfg);
}

public readonly record struct VolatilityMetrics(decimal Sigma, decimal DisplayScale, int TickCount);
