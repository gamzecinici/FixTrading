using System.Collections.Concurrent;
using FixTrading.Common.Dtos.Alert;
using FixTrading.Domain.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace FixTrading.Infrastructure.Email;

// FIX → FixApp → Handler → PricingAlertChecker → IAlertNotifier → EmailAlertNotifier → MAIL
public class EmailAlertNotifier : IAlertNotifier
{
    private readonly IOptionsMonitor<EmailAlertOptions> _optionsMonitor;
    private readonly ConcurrentDictionary<string, DateTime> _lastSentAt = new();
    // Auth hatası sonrası belirli bir süre sessiz kal; bu süre geçince tekrar dene.
    private DateTime _authErrorSilentUntil = DateTime.MinValue;

    public EmailAlertNotifier(IOptionsMonitor<EmailAlertOptions> optionsMonitor)
    {
        _optionsMonitor = optionsMonitor;
    }

    public async Task NotifyAsync(DtoAlert alert, CancellationToken ct = default)
    {
        var opts = _optionsMonitor.CurrentValue;
        if (!opts.Enabled || string.IsNullOrWhiteSpace(opts.ToAddresses))
            return;

        if (DateTime.UtcNow < _authErrorSilentUntil)
            return;

        var key = $"{alert.Symbol}|{alert.Type}";
        var cooldown = TimeSpan.FromMinutes(Math.Max(1, opts.AlertCooldownMinutes));
        if (_lastSentAt.TryGetValue(key, out var last) && DateTime.UtcNow - last < cooldown)
            return;

        var toList = opts.ToAddresses
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .ToList();
        if (toList.Count == 0) return;

        var message = CreateMimeMessage(alert, opts, toList);
        var retries = Math.Max(0, opts.RetryCount);

        for (var attempt = 0; attempt <= retries; attempt++)
        {
            if (attempt > 0)
                await Task.Delay(TimeSpan.FromSeconds(30 * attempt), ct);

            using var client = new SmtpClient();
            try
            {
                // Bağlantı zaman aşımı süresini artıralım (Gmail bazen yavaş cevap verebilir)
                client.Timeout = 15000; 

                var secureSocketOptions = opts.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None;
                await client.ConnectAsync(opts.SmtpHost, opts.SmtpPort, secureSocketOptions, ct);

                if (!string.IsNullOrEmpty(opts.Username))
                {
                    var user = opts.Username.Trim();
                    var pass = (opts.Password ?? string.Empty).Trim().Replace(" ", "");
                    
                    // Log'u biraz daha detaylandıralım (Şifrenin ilk ve son karakterini göstererek kontrol sağlayalım)
                    if (pass.Length != 16)
                    {
                        Console.WriteLine($"[EmailAlertNotifier] KRİTİK HATA: Gmail App Password 16 hane olmalı. Sizin şifreniz {pass.Length} hane.");
                    }

                    await client.AuthenticateAsync(user, pass, ct);
                }

                await client.SendAsync(message, ct);
                _lastSentAt[key] = DateTime.UtcNow;
                _authErrorSilentUntil = DateTime.MinValue;
                Console.WriteLine($"[EmailAlertNotifier] Başarılı: {alert.Symbol} maili gönderildi.");
                return;
            }
            catch (Exception ex)
            {
                var msg = ex.Message;
                if (msg.Contains("535") || msg.Contains("5.7.8") || msg.Contains("BadCredentials") || msg.Contains("not accepted"))
                {
                    _authErrorSilentUntil = DateTime.UtcNow.AddMinutes(5); // 30 dakika çok uzun, test için 5 dakikaya indirdim.
                    Console.WriteLine($"[EmailAlertNotifier] GMAIL REDDETTİ: '{opts.Username}' için şifre kabul edilmedi.");
                    Console.WriteLine($"[EmailAlertNotifier] ÇÖZÜM: Google Hesabı -> Güvenlik -> 2 Adımlı Doğrulama -> Uygulama Şifreleri kısmından 'YENİ' bir şifre alıp appsettings'e yapıştırın.");
                    Console.WriteLine($"[EmailAlertNotifier] Hata detayı: {msg}");
                    return;
                }

                if (attempt == retries)
                {
                    Console.WriteLine($"[EmailAlertNotifier] E-posta gönderilemedi: {msg}");
                }
            }
            finally
            {
                try { await client.DisconnectAsync(true, ct); } catch { }
            }
        }
    }

    private MimeMessage CreateMimeMessage(DtoAlert alert, EmailAlertOptions opts, List<string> toList)
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
