namespace FixTrading.Domain.Entities;

//Sistemde kullanilan enstrümanlarin fiyatlandırma limitlerini tutan entity
public sealed class SymbolPricingLimit
{
    public string Symbol { get; set; } = string.Empty;                 //Enstruman sembolu

    public decimal MinMid { get; set; }                               //Enstrumanin mid fiyatinin alabilecegi minimum deger

    public decimal MaxMid { get; set; }                               //Enstrumanin mid fiyatinin alabilecegi maximum deger

    public decimal MaxSpread { get; set; }                            //Enstrumanin spreadinin alabilecegi maximum deger

}
