using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FixTrading.Common.Dtos.Order;

/// <summary>
/// DTO sınıfları için ortak audit alanları.
/// DtoOrder EF entity olarak kullanıldığında tablo sütunlarına eşlenir.
/// </summary>
public class DtoBase
{
    [Column("record_date")]
    public DateTime RecordDate { get; set; }

    /// <summary>Audit kullanıcı. varchar(50), nullable</summary>
    [Column("record_user")]
    [MaxLength(50)]
    public string? RecordUser { get; set; }

    [Column("record_create_date")]
    public DateTime RecordCreateDate { get; set; }
}
