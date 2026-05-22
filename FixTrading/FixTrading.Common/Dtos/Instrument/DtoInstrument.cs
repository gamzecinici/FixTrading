using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FixTrading.Common.Dtos.Order;

namespace FixTrading.Common.Dtos.Instrument;

[Table("instruments")]
public class DtoInstrument : DtoBase
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("symbol")]
    [MaxLength(20)]
    public string Symbol { get; set; } = string.Empty;

    [Column("base")]
    [MaxLength(10)]
    public string? Base { get; set; }

    [Column("quote")]
    [MaxLength(10)]
    public string? Quote { get; set; }
}
