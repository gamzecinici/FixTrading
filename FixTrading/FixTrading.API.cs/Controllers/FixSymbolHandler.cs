using FixTrading.Application.Services;
using FixTrading.Common.Dtos.FixSymbol;

namespace FixTrading.API.Controllers;

/// <summary>
/// İç kullanım için FixSymbol işlemlerini yöneten handler.
/// </summary>
public class FixSymbolHandler
{
    private readonly IFixSymbolService _fixSymbolService;

    public FixSymbolHandler(IFixSymbolService fixSymbolService)
    {
        _fixSymbolService = fixSymbolService;
    }

    public Task<List<DtoFixSymbol>> RetrieveAllFixSymbolsAsync()
        => _fixSymbolService.RetrieveAllFixSymbolsAsync();

    public Task<DtoFixSymbol?> RetrieveFixSymbolByIdAsync(long fixSymbolId)
        => _fixSymbolService.RetrieveFixSymbolByIdAsync(fixSymbolId);

    public Task CreateNewFixSymbolAsync(DtoFixSymbol fixSymbol)
        => _fixSymbolService.CreateNewFixSymbolAsync(fixSymbol);

    public Task UpdateExistingFixSymbolAsync(long fixSymbolId, DtoFixSymbol fixSymbol)
        => _fixSymbolService.UpdateExistingFixSymbolAsync(fixSymbolId, fixSymbol);

    public Task DeleteFixSymbolByIdAsync(long fixSymbolId)
        => _fixSymbolService.DeleteFixSymbolByIdAsync(fixSymbolId);
}

