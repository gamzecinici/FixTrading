using Microsoft.AspNetCore.Mvc;
using FixTrading.Application.Interfaces.Instrument;
using FixTrading.Common.Dtos.Instrument;

namespace FixTrading.API.Controllers;

/// <summary>
/// Test ve geliştirme amaçlı HTTP API endpoint'leri.
/// IInstrumentService interface altında çalışır; iş mantığı içermez.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
    private readonly IInstrumentService _instrumentService;

    public TestController(IInstrumentService instrumentService)
    {
        _instrumentService = instrumentService;
    }

    /// <summary>Veritabanı bağlantısını test eder; kayıt sayısını döner.</summary>
    [HttpGet("db-test")]
    public async Task<IActionResult> TestDatabaseConnection()
    {
        var instruments = await _instrumentService.RetrieveAllInstrumentsAsync();
        return Ok($"Sistem çalışıyor. Instrument sayısı: {instruments.Count}");
    }

    /// <summary>Tüm Instrument kayıtlarını listeler.</summary>
    [HttpGet("list")]
    public async Task<IActionResult> GetAllInstruments()
    {
        var instruments = await _instrumentService.RetrieveAllInstrumentsAsync();
        return Ok(instruments);
    }

    /// <summary>Yeni Instrument ekler.</summary>
    [HttpPost("add")]
    public async Task<IActionResult> AddInstrument([FromBody] DtoInstrument instrument)
    {
        await _instrumentService.CreateNewInstrumentAsync(instrument);
        return Ok("Kayıt başarıyla eklendi.");
    }

    /// <summary>Mevcut Instrument kaydını günceller.</summary>
    [HttpPut("update/{id:guid}")]
    public async Task<IActionResult> UpdateInstrument(Guid id, [FromBody] DtoInstrument instrument)
    {
        await _instrumentService.UpdateExistingInstrumentAsync(id, instrument);
        return Ok("Kayıt güncellendi.");
    }

    /// <summary>Instrument kaydını siler.</summary>
    [HttpDelete("delete/{id:guid}")]
    public async Task<IActionResult> DeleteInstrument(Guid id)
    {
        await _instrumentService.DeleteInstrumentByIdAsync(id);
        return Ok("Kayıt silindi.");
    }
}
