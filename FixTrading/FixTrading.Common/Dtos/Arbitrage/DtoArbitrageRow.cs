namespace FixTrading.Common.Dtos.Arbitrage;

// Arbitraj tablosunun tek bir satirini temsil eder.
// Bir ana parite (A/B) icin secili karsi parite (B/C veya C/B) uzerinden beklenen fiyat, piyasa fiyati,
// fark yuzdesi ve sinyal bilgisi tutulur. UI dropdown'u icin uygun karsi parite listesi de burada bulunur.
public sealed class DtoArbitrageRow
{
    // Ana parite sembolu (orn: "EURUSD"). Instrument tablosundan gelir.
    public string MainSymbol { get; set; } = string.Empty;

    // Secili karsi parite sembolu (orn: "USDTRY"). Dropdown'dan gelir, default USDTRY ya da ilk uygun.
    public string CounterSymbol { get; set; } = string.Empty;

    // Bu ana parite icin dropdown'da listelenecek uygun karsi pariteler (ortak para birimi sart).
    public List<string> AvailableCounters { get; set; } = [];

    // Capraz carpim sonucu olusan turev paritenin sembolu (orn: "EURTRY"). Piyasa fiyati bu semboldan okunur.
    public string DerivedSymbol { get; set; } = string.Empty;

    // Beklenen fiyat: A/B * B/C = A/C. Karsi parite ters ise (C/B) once 1/(C/B) ile cevrilir.
    public decimal? ExpectedPrice { get; set; }

    // Piyasa fiyati: DerivedSymbol icin son bilinen fiyat. Yoksa null.
    public decimal? MarketPrice { get; set; }

    // Fark (%) = ((MarketPrice - ExpectedPrice) / ExpectedPrice) * 100. Hesaplanamiyorsa null.
    public decimal? DiffPercent { get; set; }

    // Sinyal: "SAT" (% > 0.2), "AL" (% < -0.2), "Firsat yok" (diger). UI'da gosterim icin hazir metin.
    public string Signal { get; set; } = "Fırsat yok";

    // Sinyal anahtari: "sell" | "buy" | "none". UI'da renk/badge sinifi icin kullanilir.
    public string SignalKey { get; set; } = "none";
}
