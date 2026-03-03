using FixTrading.Common.Dtos.Instrument;

namespace FixTrading.Application.Interfaces.Instrument;

//Bu interface, enstrümanlarla ilgili temel operasyonları tanımlar.
//Enstrümanlar genellikle finansal araçları temsil eder ve bu servis, enstrümanların yönetimi için gerekli işlemleri sağlar.
public interface IInstrumentService
{
    Task<List<DtoInstrument>> RetrieveAllInstrumentsAsync();   //Tüm enstrümanları asenkron olarak getirir.
    Task<DtoInstrument?> RetrieveInstrumentByIdAsync(Guid id);  //Belirli bir enstrümanı ID'sine göre asenkron olarak getirir. ID'ye sahip enstrüman bulunamazsa null döner.
    Task CreateNewInstrumentAsync(DtoInstrument instrument);  //Yeni bir enstrüman oluşturur. 
    Task UpdateExistingInstrumentAsync(Guid id, DtoInstrument instrument);  //Mevcut bir enstrümanı ID'sine göre günceller. ID'ye sahip enstrüman bulunamazsa hiçbir işlem yapmaz.
    Task DeleteInstrumentByIdAsync(Guid id);  //ID'ye göre bir enstrümanı siler. ID'ye sahip enstrüman bulunamazsa hiçbir işlem yapmaz.
}
