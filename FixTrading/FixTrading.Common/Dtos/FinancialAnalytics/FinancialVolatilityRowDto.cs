namespace FixTrading.Common.Dtos.FinancialAnalytics;


// Bir sembolün volatilite durumunu tek satırda özetlemek icin kullanilir. Volatilite skoru, seviyeleri ve UI etiketleri içerir.
public sealed class FinancialVolatilityRowDto
{
    // Sembol adi (örn: "AAPL").
    public string Symbol { get; set; } = string.Empty;

    // Gosterge: getiri std sapmasi * 10000 (okunabilir olcek).
    public decimal VolatilityValue { get; set; }

    //Sistemde tanimli volatilite seviyelerinden biri. Ornegin: "dusuk", "normal", "yuksek".
    public string LevelKey { get; set; } = "normal";

    //UI etiketi: Dusuk / Normal / Yuksek.
    public string LevelLabel { get; set; } = "Normal";

    // Volatilite durumunun ne oldugunu, neden o seviyede oldugunu ve kullanicinin ne yapmasi gerektigini ozetleyen metin.
    public string SummaryWhat { get; set; } = "";

    // Volatilite durumunun kullaniciya etkisi. Ornegin, "Yuksek volatilite, fiyat dalgalanmasi bekleniyor".
    public string ImpactLevel { get; set; } = "";

    // Kullaniciya onerilen aksiyon. Ornegin, "Pozisyonu azaltmayi dusunun" veya "Daha fazla bilgi icin tiklayin".
    public string RecommendedAction { get; set; } = "";
}
