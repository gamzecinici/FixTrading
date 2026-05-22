using FixTrading.Common.Dtos.FinancialAnalytics;

//Bu satır, finansal analiz DTO sınıflarını projeye dahil eder.
namespace FixTrading.Application.Interfaces.FinancialAnalytics;

//IAIFinanceAssistantService arayüzü, finansal analiz verilerini kullanarak kullanıcı sorularına yanıt veren bir yapay zeka finans asistanı hizmeti sağlar.
//Bu arayüz, kullanıcının sorduğu soruya göre finansal analiz verilerini yorumlayarak anlamlı bir yanıt oluşturmak için bir yöntem tanımlar.
public interface IAIFinanceAssistantService
{
    // Kullanıcının sorusunu finansal analiz verileriyle birleştirerek anlamlı bir yanıt oluşturur.
    // Gerçek implementasyon OpenAI Chat Completions API'sini kullanır (Infrastructure katmanı).
    Task<AIAssistantResponseDto> GenerateResponseAsync(string userQuestion, FinancialAnalyticsSnapshotDto context, string? selectedSymbol = null);
}
