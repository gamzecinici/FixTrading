namespace FixTrading.Common.Dtos.Options;

// FIX market data ayarlarini okumak icin kullanilan config sinifi
public class FixMarketDataOptions
{
    public const string SectionName = "FixMarketData";

    // FIX'e sembol gonderirken slash kullanilsin mi?
    // true  => EURUSD yerine EUR/USD gonderilir
    // false => EURUSD olarak gonderilir
    public bool UseSlashSymbolFormat { get; set; } = true;

    // FIX baglantisi kurulduktan sonra, logon mesaji gonderildikten sonra kac saniye beklenmesi gerektigi
    public int PostLogonDelaySeconds { get; set; } = 3;

    // Varsayilan FIX kimlik bilgileri
    public string Username { get; set; } = "FINTECHEE";
    public string Password { get; set; } = "fintechee123";

    /// <summary>
    /// Dolu ise fix.cfg icindeki SocketConnectHost uzerine yazilir (or. Stunnel: 127.0.0.1).
    /// </summary>
    public string? SocketConnectHost { get; set; }

    /// <summary>
    /// Dolu ise fix.cfg icindeki SocketConnectPort uzerine yazilir.
    /// </summary>
    public int? SocketConnectPort { get; set; }
}
