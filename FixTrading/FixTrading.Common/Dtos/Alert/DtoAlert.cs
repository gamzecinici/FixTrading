namespace FixTrading.Common.Dtos.Alert;

// MongoDB "alerts" collection'a yazılan alert dokümanı.
// Limit ihlali olduğunda log olarak saklanır.
public class DtoAlert
{
    public string Symbol { get; set; } = string.Empty;  // Sembol adı, örneğin "EURUSD"
    public string Type { get; set; } = string.Empty;   // MID_TOO_LOW | MID_TOO_HIGH | SPREAD_LIMIT
    public decimal Value { get; set; }                 // İhlal edilen değerin kendisi, örneğin MID fiyatı veya spread değeri
    public decimal Limit { get; set; }                 // İhlal edilen limit değeri, örneğin MaxMid veya MaxSpread
    public DateTime Time { get; set; }                 // İhlalin gerçekleştiği zaman, UTC format
    public DateTime TimeTurkey { get; set; }            // İhlalin gerçekleştiği zaman, Türkiye (UTC+3)
}
