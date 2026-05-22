namespace FixTrading.Common.ViewModels.Admin;

/// Fiyatlandirma limitlerini gostermek icin kullanilan ViewModel.
public class PricingLimitRowVm
{
    public Guid Id { get; set; }
    public Guid InstrumentId { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public decimal MinMid { get; set; }
    public decimal MaxMid { get; set; }
    public decimal MaxSpread { get; set; }
}
