using System.Collections.Concurrent;
using FixTrading.Application.Interfaces;
using FixTrading.Application.Interfaces.Alerts;
using FixTrading.Common.Dtos.Alert;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace FixTrading.Infrastructure.Email;

// FIX → FixApp → Handler → PricingAlertChecker → IAlertNotifier → EmailAlertNotifier → MAIL
public class EmailAlertNotifier : IAlertNotifier
{
    private readonly IOptionsMonitor<EmailAlertOptions> _optionsMonitor;
    private readonly ISystemParameterService _systemParameterService;
    private readonly ConcurrentDictionary<string, DateTime> _lastSentAt = new();
    // Auth hatası sonrası belirli bir süre sessiz kal; bu süre geçince tekrar dene.
    private DateTime _authErrorSilentUntil = DateTime.MinValue;

    public EmailAlertNotifier(IOptionsMonitor<EmailAlertOptions> optionsMonitor, ISystemParameterService systemParameterService)
    {
        _optionsMonitor = optionsMonitor;
        _systemParameterService = systemParameterService;
    }

    public async Task NotifyAsync(DtoAlert alert, CancellationToken ct = default)
    {
        var opts = _optionsMonitor.CurrentValue;
        await ApplyDynamicOptionsAsync(opts);

        var key = $"{alert.Symbol}|{alert.Type}";
        if (ShouldSkipNotification(opts, key))
            return;

        var toList = BuildRecipientList(opts.ToAddresses);
        if (toList.Count == 0) return;

        var message = CreateMimeMessage(alert, opts, toList);
        await SendWithRetriesAsync(message, opts, key, alert.Symbol, ct);
    }

    private async Task ApplyDynamicOptionsAsync(EmailAlertOptions opts)
    {
        try
        {
            // Dinamik olarak sistem parametrelerinden yapilandirmayi alalim.
            var config = await _systemParameterService.GetConfigAsync("EmailAlert");
            if (config != null)
                ApplyConfig(opts, config);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EmailAlertNotifier] Dinamik email parametreleri okunamadı: {ex.Message}");
        }
    }

    private static void ApplyConfig(EmailAlertOptions opts, Dictionary<string, string> config)
    {
        if (config.TryGetValue("SmtpHost", out var host) && host != null) opts.SmtpHost = host;
        if (config.TryGetValue("SmtpPort", out var portStr) && int.TryParse(portStr, out var port)) opts.SmtpPort = port;
        if (config.TryGetValue("AlertCooldownMinutes", out var cooldownStr) && int.TryParse(cooldownStr, out var cooldownVal)) opts.AlertCooldownMinutes = cooldownVal;
        if (config.TryGetValue("RetryCount", out var retryStr) && int.TryParse(retryStr, out var retry)) opts.RetryCount = retry;
        if (config.TryGetValue("FromName", out var name) && name != null) opts.FromName = name;
        if (config.TryGetValue("UseSsl", out var sslStr) && bool.TryParse(sslStr, out var ssl)) opts.UseSsl = ssl;
    }

    private bool ShouldSkipNotification(EmailAlertOptions opts, string key)
    {
        if (!opts.Enabled || string.IsNullOrWhiteSpace(opts.ToAddresses))
            return true;

        if (DateTime.UtcNow < _authErrorSilentUntil)
            return true;

        var cooldown = TimeSpan.FromMinutes(Math.Max(1, opts.AlertCooldownMinutes));
        return _lastSentAt.TryGetValue(key, out var last) && DateTime.UtcNow - last < cooldown;
    }

    private static List<string> BuildRecipientList(string toAddresses)
        => toAddresses
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .ToList();

    private async Task SendWithRetriesAsync(MimeMessage message, EmailAlertOptions opts, string key, string symbol, CancellationToken ct)
    {
        var retries = Math.Max(0, opts.RetryCount);
        for (var attempt = 0; attempt <= retries; attempt++)
        {
            if (attempt > 0)
                await Task.Delay(TimeSpan.FromSeconds(30 * attempt), ct);

            var sent = await TrySendAsync(message, opts, key, symbol, attempt == retries, ct);
            if (sent)
                break;
        }
    }

    private async Task<bool> TrySendAsync(
        MimeMessage message,
        EmailAlertOptions opts,
        string key,
        string symbol,
        bool isLastAttempt,
        CancellationToken ct)
    {
        using var client = new SmtpClient { Timeout = 15000 };
        try
        {
            await ConnectAndAuthenticateAsync(client, opts, ct);
            await client.SendAsync(message, ct);
            _lastSentAt[key] = DateTime.UtcNow;
            _authErrorSilentUntil = DateTime.MinValue;
            Console.WriteLine($"[EmailAlertNotifier] Başarılı: {symbol} maili gönderildi.");
            return true;
        }
        catch (Exception ex)
        {
            HandleSendFailure(ex, opts, isLastAttempt);
            return IsAuthenticationFailure(ex.Message);
        }
        finally
        {
            await DisconnectQuietlyAsync(client, ct);
        }
    }

    private static async Task ConnectAndAuthenticateAsync(SmtpClient client, EmailAlertOptions opts, CancellationToken ct)
    {
        var secureSocketOptions = opts.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None;
        await client.ConnectAsync(opts.SmtpHost, opts.SmtpPort, secureSocketOptions, ct);

        if (string.IsNullOrEmpty(opts.Username))
            return;

        var user = opts.Username.Trim();
        var pass = (opts.Password ?? string.Empty).Trim().Replace(" ", "");

        if (pass.Length != 16)
            Console.WriteLine($"[EmailAlertNotifier] KRİTİK HATA: Gmail App Password 16 hane olmalı. Sizin şifreniz {pass.Length} hane.");

        await client.AuthenticateAsync(user, pass, ct);
    }

    private void HandleSendFailure(Exception ex, EmailAlertOptions opts, bool isLastAttempt)
    {
        var msg = ex.Message;
        if (IsAuthenticationFailure(msg))
        {
            _authErrorSilentUntil = DateTime.UtcNow.AddMinutes(5);
            Console.WriteLine($"[EmailAlertNotifier] GMAIL REDDETTİ: '{opts.Username}' için şifre kabul edilmedi.");
            Console.WriteLine("[EmailAlertNotifier] ÇÖZÜM: Google Hesabı -> Güvenlik -> 2 Adımlı Doğrulama -> Uygulama Şifreleri kısmından 'YENİ' bir şifre alıp appsettings'e yapıştırın.");
            Console.WriteLine($"[EmailAlertNotifier] Hata detayı: {msg}");
            return;
        }

        if (isLastAttempt)
            Console.WriteLine($"[EmailAlertNotifier] E-posta gönderilemedi: {msg}");
    }

    private static bool IsAuthenticationFailure(string message)
        => message.Contains("535") ||
           message.Contains("5.7.8") ||
           message.Contains("BadCredentials") ||
           message.Contains("not accepted");

    private static async Task DisconnectQuietlyAsync(SmtpClient client, CancellationToken ct)
    {
        try
        {
            await client.DisconnectAsync(true, ct);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EmailAlertNotifier] SMTP disconnect hatası yok sayıldı: {ex.Message}");
        }
    }

    private static MimeMessage CreateMimeMessage(DtoAlert alert, EmailAlertOptions opts, List<string> toList)
    {
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
        message.From.Add(new MailboxAddress(opts.FromName, opts.FromAddress));
        foreach (var to in toList)
            message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };
        return message;
    }
}
