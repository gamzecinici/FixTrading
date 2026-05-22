using FixTrading.Application.Interfaces.FinancialAnalytics;
using FixTrading.Common.Dtos.FinancialAnalytics;

namespace FixTrading.Application.Services;

//Volalite metriklerini hesaplayan ve sınıflandıran servis.
// Veritabanına ihtiyaç duymadan yalnızca gelen fiyat verileri üzerinden
// volatilite hesaplaması yapan saf (pure) servis sınıfı.
// sealed kullanılması:
// Bu sınıf başka bir sınıf tarafından kalıtım alınamaz (inherit edilemez).
public sealed class VolatilityAnalyticsService : IVolatilityAnalyticsService
{
    //Gelen mid fiyatları üzerinden volatilite hesaplanır
    public VolatilityMetrics ComputeMetrics(IReadOnlyList<decimal> mids, Dictionary<string, string> cfg)
    {
        //Eger yeterli fiyat verisi yoksa volatilite sıfır olarak döndürülür
        if (mids.Count < 2)
            return new VolatilityMetrics(0m, 0m, mids.Count);

        var rets = new List<double>(mids.Count - 1);    //4 fiyat varsa 3 getiri hesaplanır
        for (var i = 1; i < mids.Count; i++)            //2. elemandan baslayarak her fiyatın bir önceki fiyata göre getirisini hesaplar
        {
            var prev = mids[i - 1];
            if (prev == 0) continue;
            //getiri hesaplanir. Formül:
            // (Yeni Fiyat - Eski Fiyat) / Eski Fiyat
            rets.Add((double)((mids[i] - prev) / prev));               
        }

        if (rets.Count == 0)
            return new VolatilityMetrics(0m, 0m, mids.Count);

        var avg = rets.Average();
        var variance = rets.Sum(r => (r - avg) * (r - avg)) / rets.Count;            //Varyans hesaplanır.
        var sigma = (decimal)Math.Sqrt(variance);                                    // Standart sapma (sigma) hesaplanır. Varyansın karekökü olarak bulunur.
        decimal multiplier = TryGet(cfg, "VolBpsMultiplier", 10000m);
        return new VolatilityMetrics(sigma, sigma * multiplier, mids.Count);
    }

    // Hesaplanan volatilite metriklerine göre sembolün volatilite seviyesini sınıflandırır.
    public FinancialVolatilityRowDto BuildRow(string symbol, VolatilityMetrics metrics, Dictionary<string, string> cfg)
    {
        if (metrics.TickCount < 2)
        {
            return new FinancialVolatilityRowDto
            {
                Symbol = symbol,
                VolatilityValue = 0m,
                LevelKey = "normal",
                LevelLabel = "Normal",
                SummaryWhat = "Yeterli veri yok.",
                ImpactLevel = "Normal",
                RecommendedAction = "Veri akisini izleyin."
            };
        }

        // Volatilite seviyesini sınıflandırmak için ClassifyVolatility metodunu çağırır.
        var (levelKey, levelLabel) = ClassifyVolatility(metrics.DisplayScale, cfg);
        return new FinancialVolatilityRowDto
        {
            Symbol = symbol,
            VolatilityValue = decimal.Round(metrics.DisplayScale, 4),
            LevelKey = levelKey,
            LevelLabel = levelLabel,
            SummaryWhat = VolSummary(levelKey),
            ImpactLevel = levelLabel,
            RecommendedAction = VolAction(levelKey, cfg)
        };
    }

    // Volatilite seviyesini belirlemek için kullanılan yardımcı metot.
    private static (string Key, string Label) ClassifyVolatility(decimal displayScale, Dictionary<string, string> cfg)
    {
        decimal low = TryGet(cfg, "VolLowThreshold", 4m);
        decimal normal = TryGet(cfg, "VolNormalThreshold", 12m);
        if (displayScale < low) return ("dusuk", "Düşük");
        if (displayScale < normal) return ("normal", "Normal");
        return ("yuksek", "Yüksek");
    }

    private static string VolSummary(string levelKey)
    {
        return levelKey switch
        {
            "dusuk" => "Volatilite dusuk seviyede.",
            "yuksek" => "Volatilite yuksek seviyede.",
            _ => "Volatilite normal seviyede."
        };
    }

    private static string VolAction(string levelKey, Dictionary<string, string> cfg)
    {
        if (levelKey == "yuksek")
            return cfg.TryGetValue("VolHighAction", out var high) ? high : "Pozisyonu azaltin ve spreadi takip edin.";
        if (levelKey == "normal")
            return cfg.TryGetValue("VolNormalAction", out var normal) ? normal : "Islem oncesi spreadi kontrol edin.";
        return "Rutin takip yeterli.";
    }

    // Verilen anahtar için yapılandırma sözlüğünden ondalık değeri almaya çalışan yardımcı metot.
    private static decimal TryGet(Dictionary<string, string> cfg, string key, decimal defaultValue)
    {
        if (cfg.TryGetValue(key, out var val) && decimal.TryParse(val, out var res)) return res;
        return defaultValue;
    }
}
