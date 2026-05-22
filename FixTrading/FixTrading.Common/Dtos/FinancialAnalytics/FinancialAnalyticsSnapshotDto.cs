using FixTrading.Common.Dtos.Arbitrage;

namespace FixTrading.Common.Dtos.FinancialAnalytics;

// Finansal analiz sonucunu tek bir nesnede toplamak icin kullanilir.
// Risk, volatilite ve arbitraj durumlarini tek istekte UI'a tasir.
public sealed class FinancialAnalyticsSnapshotDto
{
    public List<FinancialRiskRowDto> Risk { get; set; } = [];
    public List<FinancialVolatilityRowDto> Volatility { get; set; } = [];
    public ArbitrageSnapshotDto Arbitrage { get; set; } = new();

    // Anlık veriden otomatik tespit edilen anomali/uyarı kartları
    public List<AnomalyCardDto> Anomalies { get; set; } = [];
}
