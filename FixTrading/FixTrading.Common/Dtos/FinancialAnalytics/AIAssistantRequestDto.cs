namespace FixTrading.Common.Dtos.FinancialAnalytics;

// AIAssistantRequestDto sınıfı, yapay zeka finans asistanına gönderilecek istek verilerini temsil eder.
public class AIAssistantRequestDto
{
    //UserQuestion: Kullanıcının finansal analizle ilgili sorusunu temsil eder. Bu soru, yapay zeka finans asistanının yanıt oluşturmak için kullanacağı temel bilgidir.
    public string UserQuestion { get; set; } = string.Empty;

    //Kullanıcının sectigi bir sembol varsa, bu sembolü temsil eder.
    public string? SelectedSymbol { get; set; }
}
