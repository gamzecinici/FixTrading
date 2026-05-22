using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FixTrading.Persistence.Entities;

// Sistem parametrelerini temsil eden varlik sinifi

[Table("system_parameters")]            // Veritabaninda "system_parameters" tablosuna karsilik gelir

// Bu sinif, uygulamanin farkli bolumlerinde kullanilan yapilandirma parametrelerini saklamak icin kullanilir.
public class SystemParameterEntity

{
    [Key]                     // Birincil anahtar
    [Column("id")]            // Veritabaninda "id" sutununa karsilik gelir
    public int Id { get; set; }

    [Required]              // Bos gecilemez
    [MaxLength(150)]
    [Column("dosya_adi")]
    public string DosyaAdi { get; set; } = null!;          //Config'in hangi dosyaya ait oldugunu tutar

    [Required]
    [Column("config", TypeName = "jsonb")]
    public string Config { get; set; } = null!;           // Config verilerini JSON formatinda saklar. Ayarlar burada tutulur.

    [Column("olusturulma_tarihi")]
    public DateTime OlusturulmaTarihi { get; set; } = DateTime.UtcNow;     // Kaydın olusturulma tarihini tutar

    [Column("guncellenme_tarihi")]
    public DateTime GuncellenmeTarihi { get; set; } = DateTime.UtcNow;    // Kaydın son guncellenme tarihini tutar

    [Column("guncelleyen_kullanici")]
    [MaxLength(150)]
    public string? GuncelleyenKullanici { get; set; }                   // Kaydı guncelleyen kullanıcının adını tutar
}
