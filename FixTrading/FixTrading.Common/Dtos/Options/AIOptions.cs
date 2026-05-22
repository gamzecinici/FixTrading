namespace FixTrading.Common.Dtos.Options;

// Yapay zeka servis entegrasyonu için konfigürasyon seçenekleri
public sealed class AIOptions
{
    public const string SectionName = "AI";

    // AI servis API anahtarı (appsettings.json veya ortam değişkeninden okunur)
    public string ApiKey { get; set; } = string.Empty;

    // AI servis Base URL (varsayılan: Groq API)
    public string BaseUrl { get; set; } = "https://api.groq.com/openai/v1";

    // Kullanılacak model (varsayılan: llama-3.3-70b-versatile)
    public string Model { get; set; } = "llama-3.3-70b-versatile";

    // Yanıt için maksimum token sayısı
    public int MaxTokens { get; set; } = 600;
}
