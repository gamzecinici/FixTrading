using FixTrading.Application.Interfaces.FinancialAnalytics;
using FixTrading.Common.Dtos.Arbitrage;
using FixTrading.Common.Dtos.FinancialAnalytics;
using FixTrading.Common.Dtos.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;

namespace FixTrading.Infrastructure.Services;

//Infrastructure katmanında IAIFinanceAssistantService arayüzünü uygular.
//sealed class olarak tanımlanır, böylece başka sınıflar tarafından kalıtılamaz.
public sealed class GroqFinanceAssistantService : IAIFinanceAssistantService
{
    private readonly HttpClient _http;                                                          // HTTP isteklerini gönderecek HttpClient nesnesi.
    private readonly AIOptions _options;                                                        // AI servis konfigürasyon seçeneklerini tutan AIOptions nesnesi.
    private readonly ILogger<GroqFinanceAssistantService> _logger;                             // Loglama işlemleri için ILogger arayüzü.


    //Gerekli nesneleri constructor üzerinden alır ve HttpClient'ı yapılandırır.
    public GroqFinanceAssistantService(
        IOptions<AIOptions> options,
        ILogger<GroqFinanceAssistantService> logger)
    {
        _options = options.Value;                                                             //appsettings.json içindeki AI ayarlarını alır.
        _logger = logger;

        _http = new HttpClient { BaseAddress = BuildBaseUri(_options.BaseUrl) };

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        }
    }

    // Kullanıcının sorusunu ve finansal analiz verilerini alarak AI servisinden anlamlı bir yanıt oluşturur.
    public async Task<AIAssistantResponseDto> GenerateResponseAsync(
        string userQuestion,
        FinancialAnalyticsSnapshotDto context,
        string? selectedSymbol = null)
    {
        //Eger API anahtarı yapılandırılmamışsa, kullanıcıya bilgilendirici bir hata mesajı döner ve log kaydı oluşturur.
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogWarning("[Groq] API anahtarı yapılandırılmamış.");
            return ErrorResponse("AI API anahtarı henüz yapılandırılmamış. Lütfen sistem yöneticisiyle iletişime geçin.");
        }

        try
        {
            // AI servisinin anlayabileceği şekilde sistem prompt'u oluşturur. Bu prompt, kullanıcının sorusunu ve finansal verileri içeren bağlamı içerir.
            var systemPrompt = BuildSystemPrompt(context, selectedSymbol);

            var requestBody = new
            {
                model = _options.Model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userQuestion }
                },
                max_tokens = _options.MaxTokens,
                temperature = 0.4                                              // Daha tutarlı ve az yaratıcı yanıtlar için düşük bir rastgelelik değeri kullanılır.
            };

            // Groq / OpenAI uyumlu Chat Completions endpoint'i
            var url = "chat/completions";

            HttpResponseMessage response = await _http.PostAsJsonAsync(url, requestBody);
            
            // API'nin rate limit'e takılması durumunda 3 saniye bekleyip isteği tekrar denemek için eklenmiştir.
            //Bu, geçici olarak aşırı yüklenmiş sunuculara karşı daha dayanıklı bir uygulama sağlar.
            if ((int)response.StatusCode == 429)
            {
                response.Dispose();
                _logger.LogWarning("[Groq] Rate limit (429), 3 saniye sonra tekrar deneniyor...");
                await Task.Delay(3000);
                response = await _http.PostAsJsonAsync(url, requestBody);
            }

            //Eger istek başarısız olduysa, hata mesajını okuyup loglar ve kullanıcıya bilgilendirici bir hata mesajı döner.
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                response.Dispose();
                _logger.LogError("[Groq] API hatası {Status}: {Error}", (int)response.StatusCode, errorBody);
                return ErrorResponse($"AI servisinden hata alındı ({(int)response.StatusCode}). Lütfen API anahtarınızı ve model ayarlarınızı kontrol edin.");
            }

            var result = await response.Content.ReadFromJsonAsync<OpenAIChatResponse>();             //JSON cevap modele dönüştürülür.
            var text = result?.Choices?.FirstOrDefault()?.Message?.Content?.Trim()                   // AI cevabı alınır.
                       ?? "Yanıt alınamadı.";

            //AI cevabını dondurur
            return new AIAssistantResponseDto
            {
                ResponseText = text,
                Timestamp    = DateTime.UtcNow
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[Groq] HTTP bağlantı hatası.");
            return ErrorResponse("AI servisine bağlanılamadı. İnternet bağlantısını ve API erişimini kontrol edin.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Groq] Beklenmeyen hata.");
            return ErrorResponse("Yapay zeka servisi yanıt verirken beklenmeyen bir hata oluştu. Lütfen tekrar deneyin.");
        }
    }

    // Sistem prompt'u, AI modeline verilen talimatları ve bağlamı içerir. Bu, modelin kullanıcı sorusuna daha alakalı ve doğru yanıtlar üretmesine yardımcı olur.
    private static string BuildSystemPrompt(FinancialAnalyticsSnapshotDto ctx, string? selectedSymbol)
    {
        var sb = new StringBuilder();
        var isEnglish = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("en", StringComparison.OrdinalIgnoreCase);

        if (isEnglish)
        {
            sb.AppendLine("You are the AI-powered financial analytics assistant for the FixTrading platform.");
            sb.AppendLine("Your job is to give short, clear, professional English answers about risk, volatility and arbitrage");
            sb.AppendLine("using only the live market data provided below.");
            sb.AppendLine();
            sb.AppendLine("RULES:");
            sb.AppendLine("- Use only the live market data provided below; do not invent assumptions.");
            sb.AppendLine("- Keep the answer under 200 words.");
            sb.AppendLine("- Use concrete symbols, numbers and level information.");
            sb.AppendLine("- Always end with this disclaimer: 'This content is not investment advice.'");
            sb.AppendLine();

            if (!string.IsNullOrWhiteSpace(selectedSymbol))
                sb.AppendLine($"Selected symbol: {selectedSymbol}");

            AppendEnglishMarketContext(sb, ctx);
            return sb.ToString();
        }

        sb.AppendLine("Sen FixTrading platformunun yapay zeka destekli finansal analiz asistanısın.");
        sb.AppendLine("Görevin, kullanıcılara anlık piyasa verileri ışığında risk, volatilite ve arbitraj konularında");
        sb.AppendLine("kısa, net ve profesyonel Türkçe yanıtlar vermektir.");
        sb.AppendLine();
        sb.AppendLine("KURALLAR:");
        sb.AppendLine("- Yalnızca aşağıda sağlanan anlık piyasa verilerine dayan; kendi başına varsayım üretme.");
        sb.AppendLine("- Yanıtı maksimum 200 kelimeyle kısa tut.");
        sb.AppendLine("- Somut sembol adı, sayı ve seviye bilgilerini kullan.");
        sb.AppendLine("- Yanıtının sonuna mutlaka şu uyarıyı ekle: 'Bu içerik yatırım tavsiyesi değildir.'");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(selectedSymbol))
            sb.AppendLine($"Kullanıcının odaklandığı sembol: {selectedSymbol}");

        AppendTurkishMarketContext(sb, ctx);
        return sb.ToString();
    }

    private static Uri BuildBaseUri(string baseUrl)
    {
        var builder = new UriBuilder(baseUrl);
        if (!builder.Path.EndsWith('/'))
            builder.Path += '/';
        return builder.Uri;
    }

    private static void AppendTurkishMarketContext(StringBuilder sb, FinancialAnalyticsSnapshotDto ctx)
    {
        AppendTurkishRiskContext(sb, ctx);
        AppendTurkishVolatilityContext(sb, ctx);
        AppendTurkishArbitrageContext(sb, ctx);
    }

    private static void AppendTurkishRiskContext(StringBuilder sb, FinancialAnalyticsSnapshotDto ctx)
    {
        if (ctx.Risk.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("=== ANLIK RİSK VERİLERİ ===");
            foreach (var r in ctx.Risk)
            {
                sb.AppendLine($"- {r.Symbol}: Seviye={r.LevelLabel}, Risk Skoru={r.RiskScore:F2}");
                if (!string.IsNullOrWhiteSpace(r.SummaryWhat))
                    sb.AppendLine($"  Özet: {r.SummaryWhat}");
                if (!string.IsNullOrWhiteSpace(r.RecommendedAction))
                    sb.AppendLine($"  Öneri: {r.RecommendedAction}");
            }
        }
    }

    // Anlık volatilite verilerini prompt'a ekler. Bu, modelin piyasa oynaklığı hakkında bilgi sahibi olmasını sağlar.
    private static void AppendTurkishVolatilityContext(StringBuilder sb, FinancialAnalyticsSnapshotDto ctx)
    {
        if (ctx.Volatility.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("=== VOLATİLİTE VERİLERİ ===");
            foreach (var v in ctx.Volatility)
            {
                sb.AppendLine($"- {v.Symbol}: Seviye={v.LevelLabel}, Volatilite={v.VolatilityValue:F2}");
                if (!string.IsNullOrWhiteSpace(v.SummaryWhat))
                    sb.AppendLine($"  Özet: {v.SummaryWhat}");
                if (!string.IsNullOrWhiteSpace(v.RecommendedAction))
                    sb.AppendLine($"  Öneri: {v.RecommendedAction}");
            }
        }
    }

    //Arbitraj fırsatlarını prompt'a ekler. Bu, modelin mevcut arbitraj fırsatları hakkında bilgi sahibi olmasını sağlar ve kullanıcının bu fırsatlardan haberdar olmasına yardımcı olur.
    private static void AppendTurkishArbitrageContext(StringBuilder sb, FinancialAnalyticsSnapshotDto ctx)
    {
        if (ctx.Arbitrage?.Rows?.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("=== ARBİTRAJ FIRSATLARI ===");
            var active = ctx.Arbitrage.Rows.Where(r => r.SignalKey != "none").ToList();
            AppendTurkishArbitrageRows(sb, active);
        }
    }

    private static void AppendTurkishArbitrageRows(StringBuilder sb, List<DtoArbitrageRow> active)
    {
        if (active.Count == 0)
        {
            sb.AppendLine("- Şu an aktif bir arbitraj fırsatı bulunmamaktadır.");
            return;
        }

        foreach (var a in active.Take(5))
            sb.AppendLine($"- {a.MainSymbol}/{a.CounterSymbol}: Fark %{a.DiffPercent:F3}, Sinyal={a.Signal}");
    }

    private static void AppendEnglishMarketContext(StringBuilder sb, FinancialAnalyticsSnapshotDto ctx)
    {
        if (ctx.Risk.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("=== LIVE RISK DATA ===");
            foreach (var r in ctx.Risk)
                sb.AppendLine($"- {r.Symbol}: Level={ToEnglishLevel(r.LevelKey)}, Risk Score={r.RiskScore:F2}");
        }

        if (ctx.Volatility.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("=== VOLATILITY DATA ===");
            foreach (var v in ctx.Volatility)
                sb.AppendLine($"- {v.Symbol}: Level={ToEnglishLevel(v.LevelKey)}, Volatility={v.VolatilityValue:F2}");
        }

        if (ctx.Arbitrage?.Rows?.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("=== ARBITRAGE OPPORTUNITIES ===");
            var active = ctx.Arbitrage.Rows.Where(r => r.SignalKey != "none").ToList();
            if (active.Count > 0)
            {
                foreach (var a in active.Take(5))
                    sb.AppendLine($"- {a.MainSymbol}/{a.CounterSymbol}: Diff {a.DiffPercent:F3}%, Signal={ToEnglishSignal(a.SignalKey)}");
            }
            else
            {
                sb.AppendLine("- There are currently no active arbitrage opportunities.");
            }
        }
    }

    private static string ToEnglishLevel(string? levelKey)
        => levelKey switch
        {
            "dusuk" => "Low",
            "yuksek" => "High",
            _ => "Normal"
        };

    private static string ToEnglishSignal(string? signalKey)
        => signalKey switch
        {
            "buy" => "BUY",
            "sell" => "SELL",
            _ => "No opportunity"
        };

    // Hata durumlarında kullanıcıya bilgilendirici bir mesaj döndürmek için kullanılan yardımcı metot. Ayrıca, hataları loglar.
    private static AIAssistantResponseDto ErrorResponse(string message) =>
        new() { ResponseText = message, Timestamp = DateTime.UtcNow };

    // OpenAI/Groq API yanıt modelleri
    private sealed record OpenAIChatResponse(
        [property: JsonPropertyName("choices")] List<OpenAIChoice>? Choices);

    // API yanıtındaki her bir seçeneği temsil eden model. Genellikle tek bir seçenek döner, ancak API çoklu seçenekler de sunabilir.
    private sealed record OpenAIChoice(
        [property: JsonPropertyName("message")] OpenAIChatMessage? Message);

    //AI mesaj modeli
    private sealed record OpenAIChatMessage(
        [property: JsonPropertyName("role")] string? Role,
        [property: JsonPropertyName("content")] string? Content);
}
