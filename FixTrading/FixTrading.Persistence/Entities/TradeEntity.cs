using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FixTrading.Persistence.Entities;

/// <summary>
/// trades tablosunu temsil eden EF Core entity. Sadece Persistence katmanında kullanılır.
/// </summary>
[Table("trades")]
public class TradeEntity
{
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        [Column("order_id")]
        public long OrderId { get; set; }

        [Column("fill_quantity")]
        public decimal FillQuantity { get; set; }

        [Column("fill_price")]
        public decimal FillPrice { get; set; }

        [Column("trade_time")]
        public DateTime TradeTime { get; set; }

        [Column("record_date")]
        public DateTime RecordDate { get; set; }

        [Column("record_user")]
        public string? RecordUser { get; set; }

        [Column("record_create_date")]
        public DateTime RecordCreateDate { get; set; }
}
