namespace FixTrading.Common.Dtos.FinancialAnalytics;


//Bir sembolün risk durumunu tek satırda özetlemek icin kullanilir. Risk skoru, seviyeleri ve UI etiketleri içerir.
public sealed class FinancialRiskRowDto
{
    // Sembol adi (örn: "AAPL").
    public string Symbol { get; set; } = string.Empty;

    // Gosterge: risk skoru * 10000 (okunabilir olcek). Ornegin, 0.0234 risk skoru, RiskScore olarak 234 olarak gosterilir
    public decimal RiskScore { get; set; }

    //Sistemde tanimli risk seviyelerinden biri. Ornegin: "dusuk", "normal", "yuksek".
    public string LevelKey { get; set; } = "normal";

    //UI etiketi: Dusuk / Normal / Yuksek.
    public string LevelLabel { get; set; } = "Normal";

    // Risk durumunun ne oldugunu, neden o seviyede oldugunu ve kullanicinin ne yapmasi gerektigini ozetleyen metin.
    public string SummaryWhat { get; set; } = "";

    // Risk durumunun kullaniciya etkisi. Ornegin, "Yuksek risk, fiyat dalgalanmasi bekleniyor".
    public string ImpactLevel { get; set; } = "";

    // Kullaniciya onerilen aksiyon. Ornegin, "Pozisyonu azaltmayi dusunun" veya "Daha fazla bilgi icin tiklayin".
    public string RecommendedAction { get; set; } = "";
}
