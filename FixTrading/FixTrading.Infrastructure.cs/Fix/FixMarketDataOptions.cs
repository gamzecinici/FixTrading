namespace FixTrading.Infrastructure.Fix;

// FIX market data ayarlarını  okumak için kullanılan config sınıfı
public class FixMarketDataOptions
{
    // appsettings içindeki bölüm adı ("FixMarketData")
    public const string SectionName = "FixMarketData";

    // FIX'e sembol gönderirken slash kullanılsın mı?
    // true  => EURUSD yerine EUR/USD gönderilir
    // false => EURUSD olarak gönderilir
    public bool UseSlashSymbolFormat { get; set; } = true;

    // FIX bağlantısı kurulduktan sonra, logon mesajı gönderildikten sonra kaç saniye beklenmesi gerektiği
    public int PostLogonDelaySeconds { get; set; } = 3;
}