using FixTrading.Application.Contracts;

//Admin panelinde enstruman sembolleri ve fiyat limitlerini yonetmek için kullanilan servis arayuzdur
namespace FixTrading.Application.Interfaces.Admin;

//bu servis , enstruman sembolleri ve fiyat limitlerini listelemek, yeni bir sembol eklemek ve mevcut bir sembolu silmek gibi islemleri icerir
public interface IInstrumentSymbolAdminService
{

    //Admin panelinde enstruman sembolleri ve fiyat limitlerini listelemek icin kullanilir, eger kayit yoksa bos liste doner
    Task<List<AdminSymbolListItemDto>> ListAsync(CancellationToken cancellationToken = default);

    //Admin panelinde yeni bir enstruman sembolu ve fiyat limitleri eklemek icin kullanilir
    Task<(bool Ok, string? Error, AdminSymbolListItemDto? Created)> AddAsync(
        AddSymbolApiRequest request,
        string? recordUser,
        CancellationToken cancellationToken = default);

    //Admin panelinde mevcut bir enstruman sembolu ve fiyat limitlerini silmek icin kullanilir
    Task<(bool Ok, string? Error)> DeleteAsync(Guid instrumentId, CancellationToken cancellationToken = default);
}
