namespace FixTrading.Common.Pricing;

/// Fiyat hesaplama işlemleri için yardımcı sınıf
public static class PricingCalculator
{
    //Ortalama fiyat: (Bid + Ask) / 2
    public static decimal Mid(decimal bid, decimal ask)
    {
        if (bid <= 0 || ask <= 0) return 0;
        return (bid + ask) / 2;
    }

    // Spread: Ask - Bid
    public static decimal Spread(decimal bid, decimal ask)
    {
        if (bid <= 0 || ask <= 0) return 0;
        return ask - bid;
    }

    // Hem ortalama fiyatı hem de spread'i tek seferde hesaplayan yardımcı fonksiyon
    public static (decimal Mid, decimal Spread) FromBidAsk(decimal bid, decimal ask)
    {
        if (bid <= 0 || ask <= 0) return (0, 0);
        return ((bid + ask) / 2, ask - bid);
    }
}
