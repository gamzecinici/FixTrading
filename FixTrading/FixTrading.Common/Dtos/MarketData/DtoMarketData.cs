namespace FixTrading.Common.Dtos.MarketData;

/// <summary>
/// MongoDB marketData collection için normalize edilmiş veri modeli.
/// FIX'ten gelen bid/ask verisi bu formata dönüştürülür.
/// </summary>
public class DtoMarketData
{
    public string Symbol { get; set; } = string.Empty;
    public decimal Bid { get; set; }
    public decimal Ask { get; set; }
    public decimal Mid { get; set; }
    /// <summary>UTC zaman damgası (sorgu ve sıralama için)</summary>
    public DateTime Timestamp { get; set; }
    /// <summary>Görüntüleme formatı: dd/MM/yyyy HH:mm (Türkiye saati UTC+3)</summary>
    public string TimestampFormatted { get; set; } = string.Empty;
}
