namespace FixTrading.Common.Dtos.Options;

// MongoDB bağlantı bilgilerini ve buffer ayarlarını tutan sınıf. appsettings.json'dan bu değerler okunur ve DI container'a aktarılır.
public class MongoMarketDataOptions
{
    public const string SectionName = "MongoMarketData";

    public string ConnectionString { get; set; } = "mongodb://localhost:27017";
    public string DatabaseName { get; set; } = "FixTrading";
    public string CollectionName { get; set; } = "marketData";
    public int FlushIntervalSeconds { get; set; } = 60;
}
