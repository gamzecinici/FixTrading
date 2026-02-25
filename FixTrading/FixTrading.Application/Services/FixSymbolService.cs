using FixTrading.Common.Dtos.FixSymbol;
using FixTrading.Domain.Interfaces;

namespace FixTrading.Application.Services;

/// <summary>
/// FixSymbol işlemlerini yöneten uygulama servisi.
/// Repository'ye erişir; iş kuralları burada uygulanır.
/// </summary>
public class FixSymbolService : IFixSymbolService
{
    private readonly IBaseRepository<DtoFixSymbol> _fixSymbolRepository;

    public FixSymbolService(IBaseRepository<DtoFixSymbol> fixSymbolRepository)
    {
        _fixSymbolRepository = fixSymbolRepository;
    }

    /// <summary>
    /// Veritabanından tüm FixSymbol kayıtlarını çeker.
    /// </summary>
    public Task<List<DtoFixSymbol>> RetrieveAllFixSymbolsAsync()
    {
        return _fixSymbolRepository.FetchAllAsync();
    }

    /// <summary>
    /// Id ile tek FixSymbol kaydı getirir; bulunamazsa null.
    /// </summary>
    public Task<DtoFixSymbol?> RetrieveFixSymbolByIdAsync(long id)
    {
        return _fixSymbolRepository.FetchByIdAsync(id);
    }

    /// <summary>
    /// Yeni FixSymbol kaydı oluşturur.
    /// </summary>
    public Task CreateNewFixSymbolAsync(DtoFixSymbol fixSymbol)
    {
        return _fixSymbolRepository.InsertAsync(fixSymbol);
    }

    /// <summary>
    /// Mevcut FixSymbol bilgilerini günceller.
    /// </summary>
    public Task UpdateExistingFixSymbolAsync(long id, DtoFixSymbol fixSymbol)
    {
        return _fixSymbolRepository.UpdateExistingAsync(id, fixSymbol);
    }

    /// <summary>
    /// FixSymbol kaydını veritabanından siler.
    /// </summary>
    public Task DeleteFixSymbolByIdAsync(long id)
    {
        return _fixSymbolRepository.RemoveByIdAsync(id);
    }
}

