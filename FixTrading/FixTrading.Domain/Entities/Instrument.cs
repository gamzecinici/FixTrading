namespace FixTrading.Domain.Entities;

//Sistemde kullanılan enstrümanların bilgilerini tutan entity
public sealed class Instrument
{
    public Guid Id { get; set; }                                               //Enstrüman Id'si, benzersiz bir şekilde tanımlanır

    public string Symbol { get; set; } = string.Empty;                        //Enstrüman sembolu, ornegin "EUR/USD"

    public string? Base { get; set; }                                         //Enstrümanin baz para birimi, ornegin "EUR"

    public string? Quote { get; set; }                                        //Enstrümanin karşıt para birimi, ornegin "USD"           

    public DateTime RecordDate { get; set; }                                  //Enstrümanin kaydedildigi tarih ve saat bilgisi

    public string? RecordUser { get; set; }                                   //Enstrümanin kaydini yapan kullanicinin bilgisi

    public DateTime RecordCreateDate { get; set; }                           //Enstrümanin kaydının oluşturuldugu tarih ve saat bilgisi
}
