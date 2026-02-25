using Microsoft.Extensions.Logging;

namespace FixTrading.Common.Logging;

// Ortak log sınıfı
public class AppLogger<T>
{
    // Logger nesnesi (private alan küçük harf)
    private readonly ILogger<T> _logger;

    // Constructor ile logger alınır
    public AppLogger(ILogger<T> logger)
    {
        _logger = logger;
    }

    // Bilgi mesajı yazar
    public void Info(string message)
    {
        _logger.LogInformation("Bilgi: " + message);
    }

    // Hata mesajı yazar
    public void Error(string message)
    {
        _logger.LogError("Hata: " + message);
    }

    // Uyarı mesajı yazar
    public void Warning(string message)
    {
        _logger.LogWarning("Uyarı: " + message);
    }
}
