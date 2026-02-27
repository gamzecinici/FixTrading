namespace FixTrading.Common.Dtos.MarketData;

// MongoDB marketData collection'a yazılacak normalize edilmiş veri modeli.
// FIX'ten gelen bid/ask verisi bu DTO'ya dönüştürülerek saklanır.
public class DtoMarketData
{
    // İşlem gören enstrüman (örn: EURUSD)
    public string Symbol { get; set; } = string.Empty;

    // Alış fiyatı
    public decimal Bid { get; set; }

    // Satış fiyatı
    public decimal Ask { get; set; }

    // Orta fiyat (Bid + Ask) / 2
    public decimal Mid { get; set; }

    // UTC zaman damgası (DB sorgu ve sıralama için kullanılır)
    public DateTime Timestamp { get; set; }

    // Türkiye saatine göre formatlanmış zaman (UI gösterimi için)
    public string TimestampFormatted { get; set; } = string.Empty;
}