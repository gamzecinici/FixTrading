namespace FixTrading.Common.Dtos.FinancialAnalytics;

// AIAssistantResponseDto sınıfı, yapay zeka finans asistanının kullanıcıya döneceği yanıtı temsil eder.
// Bu sınıf, yanıt metnini ve yanıtın oluşturulduğu zamanı içerir.
public class AIAssistantResponseDto
{
    //ResponseText: Kullanıcıya dönecek yanıt metnini temsil eder.
    public string ResponseText { get; set; } = string.Empty;
    //Timestamp: Yanıtın oluşturulduğu zamanı temsil eder. Varsayılan olarak, yanıt oluşturulduğunda geçerli UTC zamanını alır.
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
