namespace FixTrading.Infrastructure.Fix;

public class FixMarketDataOptions
{
    public const string SectionName = "FixMarketData";

    /// <summary>
    /// true: EURUSD -> EUR/USD (çoğu FX broker). false: EURUSD olarak gönder.
    /// </summary>
    public bool UseSlashSymbolFormat { get; set; } = true;
}
