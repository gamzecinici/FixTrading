using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FixTrading.Common.Dtos.Order;

namespace FixTrading.Common.Dtos.FixSymbol;

/// <summary>
/// FixSymbol modeli. Hem API/Application katmanlarında DTO hem de Persistence'ta EF entity olarak kullanılır.
/// FixSymbol tablosuna eşlenir.
/// </summary>
[Table("FixSymbol")]
public class DtoFixSymbol : DtoBase
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("data_type")]
    public int DataType { get; set; }

    [Column("data_name")]
    public string DataName { get; set; } = string.Empty;

    [Column("start_date")]
    public DateTime StartDate { get; set; }

    [Column("end_date")]
    public DateTime EndDate { get; set; }

    [Column("status")]
    public int Status { get; set; }
}

