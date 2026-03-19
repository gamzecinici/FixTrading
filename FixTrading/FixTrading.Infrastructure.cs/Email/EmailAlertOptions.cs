namespace FixTrading.Infrastructure.Email;

//Bu sınıf, e-posta alert'lerinin yapılandırma seçeneklerini temsil eder.
//Bu seçenekler, uygulamanın appsettings.json dosyasında "EmailAlert" bölümünde tanımlanabilir.
public class EmailAlertOptions
{
    public const string SectionName = "EmailAlert";

    public bool Enabled { get; set; }
    public string SmtpHost { get; set; } = "smtp.gmail.com";
    public int SmtpPort { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "FixTrading Alerts";

    // ToAddresses, e-posta alert'lerinin gönderileceği alıcı adreslerini içerir.Virgülle ayrılmış mail adresleri şeklinde tanımlanır.
    public string ToAddresses { get; set; } = string.Empty;
}
