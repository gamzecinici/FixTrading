namespace FixTrading.Common.Dtos.FinancialAnalytics;

// Anomali & Öneri sekmesinde gösterilecek tek bir uyarı kartını temsil eder.
// Anlık piyasa verisinden (risk, volatilite, arbitraj) otomatik üretilir.
public sealed class AnomalyCardDto
{
    // Kart türü: "risk" | "vol" | "arb" — UI'da renk/ikon seçimi için kullanılır
    public string Type { get; set; } = "risk";

    // Kartın başlığı (örn: "Risk Seviyesi Yüksek")
    public string Title { get; set; } = string.Empty;

    // İlgili sembol (örn: "EURUSD")
    public string Symbol { get; set; } = string.Empty;

    // Açıklama metni — gerçek veriden üretilir
    public string Description { get; set; } = string.Empty;

    // Badge etiketi (örn: "Yüksek Risk", "Dikkat", "Fırsat")
    public string Badge { get; set; } = string.Empty;

    // Tespit zamanı (HH:mm formatında)
    public string Time { get; set; } = string.Empty;
}
