using FixTrading.Common.Dtos.FixSymbol;

namespace FixTrading.Application.Services;

/// <summary>
/// FixSymbol işlemleri için uygulama katmanı arayüzü.
/// HTTP'den bağımsızdır; iş kuralları ve orkestrasyon burada tanımlanır.
/// </summary>
public interface IFixSymbolService
{
    /// <summary>Tüm FixSymbol kayıtlarını getirir.</summary>
    Task<List<DtoFixSymbol>> RetrieveAllFixSymbolsAsync();

    /// <summary>Belirtilen id'ye sahip FixSymbol kaydını getirir; bulunamazsa null.</summary>
    Task<DtoFixSymbol?> RetrieveFixSymbolByIdAsync(long id);

    /// <summary>Yeni FixSymbol kaydı oluşturur.</summary>
    Task CreateNewFixSymbolAsync(DtoFixSymbol fixSymbol);

    /// <summary>Mevcut FixSymbol kaydını günceller.</summary>
    Task UpdateExistingFixSymbolAsync(long id, DtoFixSymbol fixSymbol);

    /// <summary>Belirtilen FixSymbol kaydını siler.</summary>
    Task DeleteFixSymbolByIdAsync(long id);
}

