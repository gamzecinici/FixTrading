using FixTrading.Common.Dtos.Instrument;

namespace FixTrading.Domain.Interfaces;

// Bu interface, enstrümanlarla ilgili temel veri erişim operasyonlarını tanımlar.

public interface IInstrumentRepository
{
    // Yeni bir enstrüman ekler.
    Task InsertAsync(DtoInstrument dto);

    // Belirli bir enstrümanı ID'sine göre getirir. ID'ye sahip enstrüman bulunamazsa null döner.
    Task<DtoInstrument?> FetchByIdAsync(Guid id);

    // Veritabanındaki tüm enstrümanları liste olarak getirir. 
    Task<List<DtoInstrument>> FetchAllAsync();

    // Mevcut kaydı günceller. ID'ye sahip enstrüman bulunamazsa hiçbir işlem yapmaz.
    Task UpdateExistingAsync(Guid id, DtoInstrument dto);

    // ID'ye göre kaydı siler. ID'ye sahip enstrüman bulunamazsa hiçbir işlem yapmaz.
    Task RemoveByIdAsync(Guid id);
}
