using FixTrading.Common.Dtos.Instrument;

namespace FixTrading.Domain.Interfaces;

/// <summary>
/// Instrument tablosu için repository arayüzü. Guid tabanlı kimlik kullanır.
/// </summary>
public interface IInstrumentRepository
{
    Task InsertAsync(DtoInstrument dto);   // Yeni bir enstrüman ekler.
    Task<DtoInstrument?> FetchByIdAsync(Guid id);  // Belirli bir enstrümanı ID'sine göre getirir. ID'ye sahip enstrüman bulunamazsa null döner.
    Task<List<DtoInstrument>> FetchAllAsync();  // Veritabanındaki tüm enstrümanları liste olarak getirir. 
    Task UpdateExistingAsync(Guid id, DtoInstrument dto);  // Mevcut kaydı günceller. ID'ye sahip enstrüman bulunamazsa hiçbir işlem yapmaz.
    Task RemoveByIdAsync(Guid id);   // ID'ye göre kaydı siler. ID'ye sahip enstrüman bulunamazsa hiçbir işlem yapmaz.
}
