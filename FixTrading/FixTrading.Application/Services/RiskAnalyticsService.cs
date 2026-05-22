using FixTrading.Application.Interfaces.FinancialAnalytics;
using FixTrading.Common.Dtos.FinancialAnalytics;
using FixTrading.Common.Dtos.MarketData;

namespace FixTrading.Application.Services;

//Risk metriklerini hesaplayan ve sınıflandıran servis.
// Veritabanına ihtiyaç duymadan yalnızca gelen fiyat verileri üzerinden
// risk hesaplaması yapan saf (pure) servis sınıfı.
public sealed class RiskAnalyticsService : IRiskAnalyticsService
{
    //Tek bir enstrüman için risk skorunu hesaplar ve uı da gosterilecek tabloyu olusturur
    public FinancialRiskRowDto BuildRow(
        string symbol,
        DtoMarketData? latest,                             // Son gelen canlı fiyat verisi
        FinancialActiveLimitRow? lim,                      // Enstrüman için tanımlanmış aktif limitler (varsa)
        VolatilityMetrics volatility,                      // Enstrümanın volatilite metrikleri
        Dictionary<string, string> cfg)                    // Risk sınıflandırma eşikleri ve katsayıları gibi yapılandırma parametreleri
    {

        // Eğer fiyat verisi yoksa, varsayılan bir risk skoru ve sınıflandırması döndür
        if (latest is null)
        {
            return new FinancialRiskRowDto
            {
                Symbol = symbol,
                RiskScore = 50m,
                LevelKey = "normal",
                LevelLabel = "Normal",
                SummaryWhat = "Canli fiyat verisi yok.",
                ImpactLevel = "Normal",
                RecommendedAction = "Veri akis kaynaklarini kontrol edin."
            };
        }
        var spreadScore = SpreadPressureScore(latest, lim);                    //Spread buyudukce risk artar
        var rangeScore = RangePressureScore(latest.Mid, lim);                 //Mid fiyat limit sınırına yaklastıkca risk artar
        decimal moveCoeff = TryGet(cfg, "RiskMoveScoreCoeff", 100000m);      //Volatilite arttıkca risk artar
        var moveScore = Math.Min(30m, volatility.Sigma * moveCoeff);      

        var raw = spreadScore + rangeScore + moveScore;
        var score = Math.Min(100m, Math.Round(raw, 2));
        var (levelKey, levelLabel) = ClassifyRisk(score, cfg);

        // Hesaplanan risk skoruna göre sınıflandırma yaparak, UI'da gösterilecek tablo satırını oluştur
        return new FinancialRiskRowDto
        {
            Symbol = symbol,
            RiskScore = score,
            LevelKey = levelKey,
            LevelLabel = levelLabel,
            SummaryWhat = RiskSummary(levelKey),
            ImpactLevel = levelLabel,
            RecommendedAction = RiskAction(levelKey, lim is not null, cfg)
        };
    }

    //Spread ve limitlere göre risk skorunu hesaplar
    // Amaç: Spread büyüklüğünden kaynaklanan risk skorunu hesaplamak.
    // Spread = Ask - Bid
    // Spread ne kadar büyükse risk o kadar yüksektir.
    private static decimal SpreadPressureScore(DtoMarketData latest, FinancialActiveLimitRow? lim)
    {
        var spread = latest.Spread;
        if (spread < 0) spread = 0;

        if (lim is not null && lim.MaxSpread > 0)
        {
            var ratio = (double)(spread / lim.MaxSpread);
            return (decimal)Math.Min(45d, ratio * 22.5d);
        }

        // Basis point hesabı: spread / mid × 10000
        if (latest.Mid == 0) return 0m;
        var bps = (double)(spread / latest.Mid * 10000m);
        return (decimal)Math.Min(45d, bps / 3d);
    }

    // Mid fiyatın MinMid ve MaxMid sınırlarına
    // ne kadar yakın olduğunu ölçmek.
    // Sınır dışına çıkarsa maksimum risk verilir.
    private static decimal RangePressureScore(decimal mid, FinancialActiveLimitRow? lim)
    {
        if (lim is null) return 0m;
        if (mid < lim.MinMid || mid > lim.MaxMid) return 45m;

        var range = lim.MaxMid - lim.MinMid;
        if (range <= 0) return 0m;

        var norm = (double)((mid - lim.MinMid) / range);
        var closenessToEdge = 2d * Math.Abs(norm - 0.5d);
        return (decimal)(closenessToEdge * 30d);
    }


    // Hesaplanan risk skorunu yapılandırma parametrelerine göre sınıflandırır
    private static (string Key, string Label) ClassifyRisk(decimal score, Dictionary<string, string> cfg)
    {
        decimal low = TryGet(cfg, "RiskLowThreshold", 34m);
        decimal normal = TryGet(cfg, "RiskNormalThreshold", 67m);
        if (score < low) return ("dusuk", "Düşük");
        if (score < normal) return ("normal", "Normal");
        return ("yuksek", "Yüksek");
    }

    private static string RiskSummary(string levelKey)
    {
        return levelKey switch
        {
            "dusuk" => "Risk seviyesi dusuk.",
            "yuksek" => "Risk seviyesi yuksek.",
            _ => "Risk seviyesi normal."
        };
    }

    private static string RiskAction(string levelKey, bool hasLimit, Dictionary<string, string> cfg)
    {
        if (levelKey == "yuksek")
        {
            if (cfg.TryGetValue("RiskHighAction", out var high))
                return high;

            return hasLimit
                ? "Limitleri ve kotasyonu gozden gecirin."
                : "Piyasayi yakindan izleyin.";
        }
        if (levelKey == "dusuk")
            return "Rutin takip yeterli.";
        return "Temkinli izlemeyi surdurun.";
    }

    // Verilen anahtar için yapılandırma sözlüğünden ondalık değeri almaya çalışan yardımcı metot.
    private static decimal TryGet(Dictionary<string, string> cfg, string key, decimal defaultValue)
    {
        if (cfg.TryGetValue(key, out var val) && decimal.TryParse(val, out var res)) return res;
        return defaultValue;
    }
}
