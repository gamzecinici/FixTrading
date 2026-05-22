namespace FixTrading.Common.Dtos.Options;

// Redis bağlantı ayarlarını tutan sınıf. ConnectionString, Redis sunucusunun adresini belirtir.
// LatestPriceTtl, en son fiyat bilgisinin Redis'te ne kadar süreyle saklanacağını belirler (null ise süresiz)
public class RedisOptions
{
    public const string SectionName = "Redis";

    public string ConnectionString { get; set; } = "localhost:6379";
    public TimeSpan? LatestPriceTtl { get; set; } = TimeSpan.FromDays(1);

    // Varsayılan değerler merkezi 
    public string KeyPrefix { get; set; } = "latest:price:";
    public string KeySet { get; set; } = "latest:price:symbols";
    public int TurkeyOffsetHours { get; set; } = 3;
}
