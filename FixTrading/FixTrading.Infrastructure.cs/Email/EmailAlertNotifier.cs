using FixTrading.Common.Dtos.Alert;
using FixTrading.Domain.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace FixTrading.Infrastructure.Email;

// Bu sınıf, IAlertNotifier arayüzünü uygulayarak e-posta yoluyla alert bildirimleri göndermek için kullanılır.
public class EmailAlertNotifier : IAlertNotifier
{
    private readonly EmailAlertOptions _options;

    public EmailAlertNotifier(IOptions<EmailAlertOptions> options)
    {
        _options = options.Value;
    }

    // Bu yöntem, verilen alert bilgilerini kullanarak e-posta bildirimleri gönderir.
    public async Task NotifyAsync(DtoAlert alert, CancellationToken ct = default)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.ToAddresses))
            return;

        var toList = _options.ToAddresses
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .ToList();
        if (toList.Count == 0) return;

        var subject = $"[FixTrading ALERT] {alert.Symbol} - {alert.Type}";
        var body = $"""
            Pricing limit ihlali bildirimi

            Sembol: {alert.Symbol}
            Tip: {alert.Type}
            Değer: {alert.Value}
            Limit: {alert.Limit}
            Zaman (UTC):     {alert.Time:yyyy-MM-dd HH:mm:ss}
            Zaman (Türkiye): {alert.TimeTurkey:yyyy-MM-dd HH:mm:ss}

            ---
            FixTrading Pricing Alert
            """;

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        foreach (var to in toList)
            message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient();
        try
        {
            var secureSocketOptions = _options.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None;
            await client.ConnectAsync(_options.SmtpHost, _options.SmtpPort, secureSocketOptions, ct);
            if (!string.IsNullOrEmpty(_options.Username))
                await client.AuthenticateAsync(_options.Username, _options.Password, ct);
            await client.SendAsync(message, ct);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EmailAlertNotifier] E-posta gönderilemedi: {ex.Message}");
        }
        finally
        {
            try { await client.DisconnectAsync(true, ct); }
            catch { /* ignore */ }
        }
    }
}
