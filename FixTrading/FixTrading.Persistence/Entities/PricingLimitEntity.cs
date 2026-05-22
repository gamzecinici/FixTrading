using FixTrading.Common.Dtos.Instrument;

namespace FixTrading.Persistence.Entities;

// Bu sınıf, pricing_limits tablosunun veritabanındaki karşılığını temsil eder.
public class PricingLimitEntity
{
    public Guid Id { get; set; }
    public Guid InstrumentId { get; set; }
    public decimal MinMid { get; set; }
    public decimal MaxMid { get; set; }
    public decimal MaxSpread { get; set; }
    public DateTime? RecordDate { get; set; }
    public string? RecordUser { get; set; }
    public DateTime? RecordCreateDate { get; set; }

    public DtoInstrument? Instrument { get; set; }
}
