namespace FixTrading.Common.Dtos.Pricing;

// PostgreSQL "pricing_limits" tablosundaki her bir satır, bir sembol için geçerli olan fiyatlandırma limitlerini temsil eder.
public class PricingLimit
{
    public string Symbol { get; set; } = string.Empty;    // Sembol adı, örneğin "EURUSD"
    public decimal MinMid { get; set; }                   // MID fiyatı için minimum limit, örneğin 1.0000
    public decimal MaxMid { get; set; }                   // MID fiyatı için maksimum limit, örneğin 1.5000
    public decimal MaxSpread { get; set; }                // Spread için maksimum limit
}
