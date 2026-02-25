using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FixTrading.Persistence.Entities;

/// <summary>
/// instruments tablosunu temsil eden EF Core entity. Sadece Persistence katmanında kullanılır.
/// </summary>
[Table("instruments")]
public class InstrumentEntity
{
        // Primary Key
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        // Enstrüman sembolü (örn: EURUSD)
        [Column("symbol")]
        public string Symbol { get; set; } = string.Empty;

        // Açıklama
        [Column("description")]
        public string Description { get; set; } = string.Empty;

        // Minimum fiyat adımı
        [Column("tick_size")]
        public decimal TickSize { get; set; }

        // Audit alanları

        [Column("record_date")]
        public DateTime RecordDate { get; set; }

        [Column("record_user")]
        public string? RecordUser { get; set; }

        [Column("record_create_date")]
        public DateTime RecordCreateDate { get; set; }
}
