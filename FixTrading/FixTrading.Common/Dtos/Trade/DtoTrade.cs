using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FixTrading.Common.Dtos.Order;

namespace FixTrading.Common.Dtos.Trade;

/// <summary>
/// Trade modeli. Hem API/Application katmanlarında DTO hem de Persistence'ta EF entity olarak kullanılır.
/// trades tablosuna eşlenir.
/// </summary>
[Table("trades")]
public class DtoTrade : DtoBase
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
}
