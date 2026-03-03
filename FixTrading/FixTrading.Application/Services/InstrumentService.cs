using FixTrading.Application.Interfaces.Instrument;
using FixTrading.Common.Dtos.Instrument;
using FixTrading.Domain.Interfaces;

namespace FixTrading.Application.Services;

// Bu sınıf, IInstrumentService arayüzünü uygulayarak enstrümanlarla ilgili işlemleri gerçekleştirir.
public class InstrumentService : IInstrumentService
{
    // Enstrüman verilerine erişmek için kullanılan repository'yi tutar.
    private readonly IInstrumentRepository _instrumentRepository;

    // DI container bu constructor'ı otomatik çağırır
    public InstrumentService(IInstrumentRepository instrumentRepository)
    {
        _instrumentRepository = instrumentRepository;
    }

    //bu metot, tüm enstrümanları asenkron olarak getirir.
    public Task<List<DtoInstrument>> RetrieveAllInstrumentsAsync()
        => _instrumentRepository.FetchAllAsync();

    //bu metot, belirli bir ID'ye sahip enstrümanı asenkron olarak getirir.
    public Task<DtoInstrument?> RetrieveInstrumentByIdAsync(Guid id)
        => _instrumentRepository.FetchByIdAsync(id);


    //bu metot, yeni bir enstrüman oluşturur ve veritabanına ekler.
    public Task CreateNewInstrumentAsync(DtoInstrument instrument)
        => _instrumentRepository.InsertAsync(instrument);

    //bu metot, mevcut bir enstrümanı günceller.
    public Task UpdateExistingInstrumentAsync(Guid id, DtoInstrument instrument)
        => _instrumentRepository.UpdateExistingAsync(id, instrument);

    //bu metot, belirli bir ID'ye sahip enstrümanı siler.
    public Task DeleteInstrumentByIdAsync(Guid id)
        => _instrumentRepository.RemoveByIdAsync(id);
}
