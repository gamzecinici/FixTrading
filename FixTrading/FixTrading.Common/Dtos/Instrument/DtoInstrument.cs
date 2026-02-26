using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FixTrading.Common.Dtos.Order;

namespace FixTrading.Common.Dtos.Instrument;

/// <summary>
/// Instrument modeli. Hem API/Application katmanlarında DTO hem de Persistence'ta EF entity olarak kullanılır.
/// instruments tablosuna eşlenir. FIX market data akışı bu tablo üzerinden yönetilir.
/// </summary>
[Table("instruments")]
public class DtoInstrument : DtoBase
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    /// <summary>Enstrüman sembolü (örn: EURUSD, USDTRY). varchar(20)</summary>
    [Column("symbol")]
    [MaxLength(20)]
    public string Symbol { get; set; } = string.Empty;

    /// <summary>Açıklama. varchar(100), nullable</summary>
    [Column("description")]
    [MaxLength(100)]
    public string? Description { get; set; }

    /// <summary>Minimum fiyat adımı. numeric(18,8)</summary>
    [Column("tick_size", TypeName = "numeric(18,8)")]
    public decimal TickSize { get; set; }
}
