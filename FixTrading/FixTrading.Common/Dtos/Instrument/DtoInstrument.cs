using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FixTrading.Common.Dtos.Order;

namespace FixTrading.Common.Dtos.Instrument;


// Enstrüman bilgilerini temsil eden DTO sınıfı. Veritabanındaki "instruments" tablosuna karşılık gelir.
[Table("instruments")]
public class DtoInstrument : DtoBase  // DtoBase sınıfından türetilir
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    //kullanılan sembol (örn: EURUSD). varchar(20), boş olamaz
    [Column("symbol")]
    [MaxLength(20)]
    public string Symbol { get; set; } = string.Empty;  // Sembolün boş olmasını engellemek için varsayılan olarak boş string atanır

   
    [Column("description")]
    [MaxLength(100)]
    public string? Description { get; set; }   //null olabilir , okunabilir değiştirilebilir

    
    [Column("tick_size", TypeName = "numeric(18,8)")]
    public decimal TickSize { get; set; }  //tick size: Enstrümanın fiyat hareketlerinin minimum adımını belirten bir değerdir.
}
