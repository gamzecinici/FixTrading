namespace FixTrading.Application.Interfaces;


// Sistem parametrelerini yonetmek icin servis arayuzu
public interface ISystemParameterService
{
    // Belirli bir dosya adi icin sistem parametrelerini getirir
    Task<Dictionary<string, string>?> GetConfigAsync(string fileName);

    // Belirli bir dosya adi icin sistem parametrelerini gunceller veya olusturur
    Task<bool> UpdateConfigAsync(string fileName, Dictionary<string, string> config, string updatedBy);

    // Tüm parametreleri Redis-first mantığıyla getirir
    Task<List<object>> GetAllParametersAsync();

    // Birden fazla dosya adi icin sistem parametrelerini toplu olarak gunceller veya olusturur
    Task<bool> BatchUpdateConfigsAsync(Dictionary<string, string> updates, string updatedBy);
}
