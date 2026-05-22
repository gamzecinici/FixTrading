namespace FixTrading.Application.Interfaces.Pricing;

//Fiyat limitleriyle ilgili cache'i yenilemek icin kullanilan servis arayuzdur
public interface IPricingLimitsSyncService
{
    //Veritabanindan aktif fiyat limitlerini cekip cache'i yenilemek icin kullanilir, bu metod genellikle uygulama baslangicinda veya fiyat limitleri guncellendiginde cagrilir,
    //eger veritabaninda aktif limit yoksa cache bosaltilir
    Task RefreshCacheFromDatabaseAsync(CancellationToken cancellationToken = default);
}
