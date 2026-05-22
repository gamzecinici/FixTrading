using FixTrading.Common.Dtos.Instrument;

namespace FixTrading.Application.Interfaces.Instrument;

//Bu interface, enstrümanlarla ilgili temel operasyonları tanımlar.
//Enstrümanlar genellikle finansal araçları temsil eder ve bu servis, enstrümanların yönetimi için gerekli işlemleri sağlar.
public interface IInstrumentService
{
    //Tüm enstrümanları asenkron olarak getirir.
    Task<List<DtoInstrument>> RetrieveAllInstrumentsAsync();

    //Belirli bir enstrümanı ID'sine göre asenkron olarak getirir. ID'ye sahip enstrüman bulunamazsa null döner.
    Task<DtoInstrument?> RetrieveInstrumentByIdAsync(Guid id);

    //Yeni bir enstrüman oluşturur. 
    Task CreateNewInstrumentAsync(DtoInstrument instrument);

    //Mevcut bir enstrümanı ID'sine göre günceller. ID'ye sahip enstrüman bulunamazsa hiçbir işlem yapmaz.
    Task UpdateExistingInstrumentAsync(Guid id, DtoInstrument instrument);

    //ID'ye göre bir enstrümanı siler. ID'ye sahip enstrüman bulunamazsa hiçbir işlem yapmaz.
    Task DeleteInstrumentByIdAsync(Guid id);  
}
