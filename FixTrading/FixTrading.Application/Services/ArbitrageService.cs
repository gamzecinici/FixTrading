using FixTrading.Application.Interfaces.Arbitrage;
using FixTrading.Common.Dtos.Arbitrage;
using FixTrading.Common.Dtos.Instrument;
using FixTrading.Common.Dtos.MarketData;

namespace FixTrading.Application.Services;

// IArbitrageService'in DB'siz, saf hesaplama implementasyonu.
//
// HESAPLAMA KURALLARI:
//  Karsi parite secim kriteri: Ana.quote == Karsi.base VEYA Ana.base == Karsi.quote
//  (Bir paritenin base'i diğerinin quote'una esit olmali.)
//
//  Durum 1 — Ana.quote == Karsi.base  (A/B × B/C = A/C)
//    Beklenen = Ana × Karsi
//    Turetilen = Ana.base / Karsi.quote
//
//  Durum 2 — Ana.base == Karsi.quote  (C/A × A/B = C/B)
//    Beklenen = Karsi × Ana  (carpim, turetilenin dogru yonu verir)
//    Turetilen = Karsi.base / Ana.quote
public sealed class ArbitrageService : IArbitrageService
{
    private const string SignalSellKey = "sell";
    private const string SignalBuyKey = "buy";
    private const string SignalNoneKey = "none";
    private sealed record ArbitrageCalculation(string DerivedBase, string DerivedQuote, decimal Expected);

    // Tum ana pariteler icin tabloyu olusturur.
    public ArbitrageSnapshotDto BuildSnapshot(
        IReadOnlyList<DtoInstrument> instruments,
        IReadOnlyList<DtoMarketData> prices,
        Dictionary<string, string>? config = null)
    {
        var preferredCounter = ResolvePreferredCounter(config);
        var signalThreshold = ResolveSignalThreshold(config);
        var priceMap = BuildPriceMap(prices);
        var normalizedInstruments = NormalizeInstruments(instruments);

        var snapshot = new ArbitrageSnapshotDto
        {
            SignalThresholdPercent = signalThreshold,
            Rows = new List<DtoArbitrageRow>(normalizedInstruments.Count)
        };

        foreach (var main in normalizedInstruments.OrderBy(i => i.Symbol))
        {
            var availableCounters = FindAvailableCounters(main, normalizedInstruments);
            var defaultCounter = PickDefaultCounter(availableCounters, preferredCounter);
            var row = ComputeRow(main, defaultCounter, availableCounters, normalizedInstruments, priceMap, signalThreshold, config);
            snapshot.Rows.Add(row);
        }

        return snapshot;
    }

    // Tek bir satiri UI tarafindan secilen karsi parite ile hesaplar.
    public DtoArbitrageRow Compute(
        string mainSymbol,
        string counterSymbol,
        IReadOnlyList<DtoInstrument> instruments,
        IReadOnlyList<DtoMarketData> prices,
        Dictionary<string, string>? config = null)
    {
        var preferredCounter = ResolvePreferredCounter(config);
        var signalThreshold = ResolveSignalThreshold(config);
        var priceMap = BuildPriceMap(prices);
        var normalizedInstruments = NormalizeInstruments(instruments);
        var main = normalizedInstruments.FirstOrDefault(i =>
            string.Equals(i.Symbol, Normalize(mainSymbol), StringComparison.OrdinalIgnoreCase));

        if (main == null)
        {
            // Ana parite instrument tablosunda yoksa bos bir satir dondur (UI "-" gosterir).
            return new DtoArbitrageRow
            {
                MainSymbol = Normalize(mainSymbol),
                CounterSymbol = string.Empty,
                Signal = ResolveSignalNoneLabel(config),
                SignalKey = SignalNoneKey
            };
        }

        var availableCounters = FindAvailableCounters(main, normalizedInstruments);

        // UI'dan gelen counter, ortak para birimi sartini saglamiyorsa default mantiga geri don.
        var chosenCounter = Normalize(counterSymbol);
        if (!availableCounters.Contains(chosenCounter))
            chosenCounter = PickDefaultCounter(availableCounters, preferredCounter);

        return ComputeRow(main, chosenCounter, availableCounters, normalizedInstruments, priceMap, signalThreshold, config);
    }

    // Tek satir hesaplamasinin ortak govdesi. BuildSnapshot ve Compute tarafindan paylasilir.
    private static DtoArbitrageRow ComputeRow(
        DtoInstrument main,
        string counterSymbol,
        List<string> availableCounters,
        IReadOnlyList<DtoInstrument> allInstruments,
        IReadOnlyDictionary<string, decimal> priceMap,
        decimal signalThreshold,
        Dictionary<string, string>? config)
    {
        var row = new DtoArbitrageRow
        {
            MainSymbol = main.Symbol,
            CounterSymbol = counterSymbol,
            AvailableCounters = availableCounters,
            Signal = ResolveSignalNoneLabel(config),
            SignalKey = SignalNoneKey
        };

        if (string.IsNullOrEmpty(counterSymbol)) return row;

        var counter = allInstruments.FirstOrDefault(i =>
            string.Equals(i.Symbol, counterSymbol, StringComparison.OrdinalIgnoreCase));
        if (counter == null) return row;

        var anaBase  = main.Base;
        var anaQuote = main.Quote;
        if (string.IsNullOrEmpty(anaBase) || string.IsNullOrEmpty(anaQuote)) return row;

        var karsiBase  = counter.Base;
        var karsiQuote = counter.Quote;
        if (string.IsNullOrEmpty(karsiBase) || string.IsNullOrEmpty(karsiQuote)) return row;

        decimal? anaPrice  = TryGetPrice(priceMap, main.Symbol);
        if (!anaPrice.HasValue || anaPrice.Value == 0) return row;

        decimal? karsiPrice = TryGetPrice(priceMap, counter.Symbol);
        if (!karsiPrice.HasValue || karsiPrice.Value == 0) return row;

        var calculation = ResolveCalculation(main, counter, anaPrice.Value, karsiPrice.Value);
        if (calculation == null) return row;

        // Turetilen base ve quote ayni olursa (orn: TRY/TRY) anlamsizdir.
        if (string.Equals(calculation.DerivedBase, calculation.DerivedQuote, StringComparison.OrdinalIgnoreCase))
            return row;

        row.DerivedSymbol = Normalize(calculation.DerivedBase + calculation.DerivedQuote);
        row.ExpectedPrice = calculation.Expected;

        // Piyasa fiyatı: turetilen paritenin anlık quoted fiyati.
        // Dogrudan bulunamazsa ters yon denenir (GBPTRY yoksa TRYGBP varsa 1/TRYGBP alinir).
        var derivedMarket = ResolveDerivedMarket(priceMap, calculation.DerivedBase, calculation.DerivedQuote);

        row.MarketPrice = derivedMarket;

        // Fark% ve sinyal: yalnizca turetilen paritenin piyasa fiyati varsa hesaplanir.
        ApplySignal(row, derivedMarket, calculation.Expected, signalThreshold, config);

        return row;
    }

    private static ArbitrageCalculation? ResolveCalculation(
        DtoInstrument main,
        DtoInstrument counter,
        decimal mainPrice,
        decimal counterPrice)
    {
        var anaBase = main.Base!;
        var anaQuote = main.Quote!;
        var karsiBase = counter.Base!;
        var karsiQuote = counter.Quote!;

        // Durum 1: Ana.quote == Karsi.base  →  A/B × B/C = A/C
        if (string.Equals(anaQuote, karsiBase, StringComparison.OrdinalIgnoreCase))
            return new ArbitrageCalculation(anaBase, karsiQuote, mainPrice * counterPrice);

        // Durum 2: Ana.base == Karsi.quote  →  C/A × A/B = C/B
        if (string.Equals(anaBase, karsiQuote, StringComparison.OrdinalIgnoreCase))
            return new ArbitrageCalculation(karsiBase, anaQuote, counterPrice * mainPrice);

        return null;
    }

    private static decimal? ResolveDerivedMarket(
        IReadOnlyDictionary<string, decimal> priceMap,
        string derivedBase,
        string derivedQuote)
    {
        var direct = TryGetPrice(priceMap, derivedBase + derivedQuote);
        if (direct.HasValue)
            return direct;

        var inverse = TryGetPrice(priceMap, derivedQuote + derivedBase);
        return inverse.HasValue && inverse.Value != 0 ? 1m / inverse.Value : null;
    }

    private static void ApplySignal(
        DtoArbitrageRow row,
        decimal? derivedMarket,
        decimal expected,
        decimal signalThreshold,
        Dictionary<string, string>? config)
    {
        if (!derivedMarket.HasValue || derivedMarket.Value == 0 || expected == 0m)
            return;

        var diff = (derivedMarket.Value - expected) / expected * 100m;
        row.DiffPercent = diff;

        if (diff > signalThreshold)
            SetSignal(row, ResolveSignalSellLabel(config), SignalSellKey);
        else if (diff < -signalThreshold)
            SetSignal(row, ResolveSignalBuyLabel(config), SignalBuyKey);
    }

    private static void SetSignal(DtoArbitrageRow row, string label, string key)
    {
        row.Signal = label;
        row.SignalKey = key;
    }

    // Verilen ana parite icin gecerli karsi parite adaylarini dondurur.
    //
    // Secim kriteri (tam olarak iki durum):
    //   Durum 1: Ana.quote == Karsi.base  →  A/B × B/C = A/C
    //   Durum 2: Ana.base  == Karsi.quote →  C/A × A/B = C/B
    //
    // Bu iki kosulun disindaki pariteler (orn: ayni quote paylasimi) DAHIL EDILMEZ.
    private static List<string> FindAvailableCounters(
        DtoInstrument main,
        IReadOnlyList<DtoInstrument> allInstruments)
    {
        var anaBase  = main.Base;
        var anaQuote = main.Quote;
        if (string.IsNullOrEmpty(anaBase) || string.IsNullOrEmpty(anaQuote))
            return new List<string>();

        return allInstruments
            .Where(i =>
                !string.Equals(i.Symbol, main.Symbol, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrEmpty(i.Base) && !string.IsNullOrEmpty(i.Quote) &&
                (
                    // Durum 1: Ana.quote == Karsi.base
                    string.Equals(anaQuote, i.Base, StringComparison.OrdinalIgnoreCase) ||
                    // Durum 2: Ana.base == Karsi.quote
                    string.Equals(anaBase, i.Quote, StringComparison.OrdinalIgnoreCase)
                ))
            .Select(i => i.Symbol)
            .Distinct()
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // Default karsi parite: USDTRY varsa o, yoksa alfabetik ilk uygun; hic yoksa bos.
    private static string PickDefaultCounter(List<string> availableCounters, string preferredCounter)
    {
        if (availableCounters.Count == 0) return string.Empty;
        var preferred = availableCounters.FirstOrDefault(s =>
            string.Equals(s, preferredCounter, StringComparison.OrdinalIgnoreCase));
        return preferred ?? availableCounters[0];
    }

    private static string GetRequiredString(Dictionary<string, string>? config, string key)
    {
        if (config != null && config.TryGetValue(key, out var val) && !string.IsNullOrWhiteSpace(val))
            return val.Trim();

        throw new InvalidOperationException($"FinancialAnalytics parametresi zorunludur: {key}");
    }

    private static decimal GetRequiredDecimal(Dictionary<string, string>? config, string key)
    {
        if (config != null &&
            config.TryGetValue(key, out var val) &&
            decimal.TryParse(val, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
            return parsed;

        throw new InvalidOperationException($"FinancialAnalytics decimal parametresi gecersiz veya eksik: {key}");
    }

    private static string ResolvePreferredCounter(Dictionary<string, string>? config)
        => GetRequiredString(config, "DefaultPreferredCounter");

    private static decimal ResolveSignalThreshold(Dictionary<string, string>? config)
        => GetRequiredDecimal(config, "SignalThreshold");

    private static string ResolveSignalSellLabel(Dictionary<string, string>? config)
        => GetRequiredString(config, "SignalSell");

    private static string ResolveSignalBuyLabel(Dictionary<string, string>? config)
        => GetRequiredString(config, "SignalBuy");

    private static string ResolveSignalNoneLabel(Dictionary<string, string>? config)
        => GetRequiredString(config, "SignalNone");

    // DtoInstrument koleksiyonunun Symbol/Base/Quote alanlarini normalize eder (buyuk harf, bosluksuz).
    // Base bos ise sembolden tahmin edilir; Quote yalnizca instrument kaydindaki alandan alinir (bos ise null).
    private static List<DtoInstrument> NormalizeInstruments(IReadOnlyList<DtoInstrument> instruments)
    {
        var result = new List<DtoInstrument>(instruments.Count);
        foreach (var i in instruments)
        {
            var symbol = Normalize(i.Symbol);
            if (string.IsNullOrEmpty(symbol)) continue;
            var baseCcy = !string.IsNullOrWhiteSpace(i.Base) ? i.Base!.Trim().ToUpperInvariant() : TryGuessBase(symbol);
            var quoteCcy = !string.IsNullOrWhiteSpace(i.Quote) ? i.Quote!.Trim().ToUpperInvariant() : null;
            result.Add(new DtoInstrument
            {
                Id = i.Id,
                Symbol = symbol,
                Base = baseCcy,
                Quote = quoteCcy
            });
        }
        return result;
    }

    // 6 karakterlik standart FX sembollerinde ilk 3 harf base kabul edilir. 
    // Ayrica kripto veya diger uzun semboller icin bilinen son ekleri kontrol eder.
    private static string? TryGuessBase(string symbol)
    {
        if (symbol.Length == 6) return symbol.Substring(0, 3);
        if (symbol.EndsWith("USDT")) return symbol.Substring(0, symbol.Length - 4);
        if (symbol.EndsWith("USD") || symbol.EndsWith("TRY") || symbol.EndsWith("BTC") || symbol.EndsWith("EUR")) 
            return symbol.Substring(0, symbol.Length - 3);
        return null;
    }

    // Piyasa verisini sembol -> mid fiyat dictionary'sine cevirir. Mid 0 ise (veya yoksa) bid/ask ortalamasi denenir.
    private static Dictionary<string, decimal> BuildPriceMap(IReadOnlyList<DtoMarketData> prices)
    {
        var map = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in prices)
        {
            var sym = Normalize(p.Symbol);
            if (string.IsNullOrEmpty(sym)) continue;
            decimal value;
            if (p.Mid > 0m) value = p.Mid;
            else if (p.Bid > 0m && p.Ask > 0m) value = (p.Bid + p.Ask) / 2m;
            else if (p.Ask > 0m) value = p.Ask;
            else if (p.Bid > 0m) value = p.Bid;
            else continue;
            map[sym] = value;
        }
        return map;
    }

    private static decimal? TryGetPrice(IReadOnlyDictionary<string, decimal> priceMap, string symbol)
        => priceMap.TryGetValue(Normalize(symbol), out var v) ? v : (decimal?)null;

    private static string Normalize(string? s)
        => (s ?? string.Empty).Trim().ToUpperInvariant().Replace("/", string.Empty);
}
