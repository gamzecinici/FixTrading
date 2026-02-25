using Microsoft.AspNetCore.Mvc;
using FixTrading.Application.Services;
using FixTrading.Common.Dtos.FixSymbol;

namespace FixTrading.API.Controllers;

/// <summary>
/// Test ve geliştirme amaçlı HTTP API endpoint'leri.
/// Sadece IFixSymbolService'e yönlendirme yapar; iş mantığı içermez.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
    private readonly IFixSymbolService _fixSymbolService;

    public TestController(IFixSymbolService fixSymbolService)
    {
        _fixSymbolService = fixSymbolService;
    }

    /// <summary>
    /// Veritabanı bağlantısını test eder; kayıt sayısını döner.
    /// </summary>
    [HttpGet("db-test")]
    public async Task<IActionResult> TestDatabaseConnection()
    {
        var symbols = await _fixSymbolService.RetrieveAllFixSymbolsAsync();
        return Ok($"Sistem çalışıyor. Kayıt sayısı: {symbols.Count}");
    }

    /// <summary>
    /// Tüm FixSymbol kayıtlarını listeler.
    /// </summary>
    [HttpGet("list")]
    public async Task<IActionResult> GetAllOrders()
    {
        var symbols = await _fixSymbolService.RetrieveAllFixSymbolsAsync();
        return Ok(symbols);
    }

    /// <summary>
    /// Yeni FixSymbol ekler.
    /// </summary>
    [HttpPost("add")]
    public async Task<IActionResult> AddOrder([FromBody] DtoFixSymbol fixSymbol)
    {
        await _fixSymbolService.CreateNewFixSymbolAsync(fixSymbol);
        return Ok("Kayıt başarıyla eklendi.");
    }

    /// <summary>
    /// Mevcut FixSymbol kaydını günceller.
    /// </summary>
    [HttpPut("update/{id}")]
    public async Task<IActionResult> UpdateOrder(long id, [FromBody] DtoFixSymbol fixSymbol)
    {
        await _fixSymbolService.UpdateExistingFixSymbolAsync(id, fixSymbol);
        return Ok("Kayıt güncellendi.");
    }

    /// <summary>
    /// FixSymbol kaydını siler.
    /// </summary>
    [HttpDelete("delete/{id}")]
    public async Task<IActionResult> DeleteOrder(long id)
    {
        await _fixSymbolService.DeleteFixSymbolByIdAsync(id);
        return Ok("Kayıt silindi.");
    }
}
