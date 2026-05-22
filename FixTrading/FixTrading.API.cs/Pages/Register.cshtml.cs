using FixTrading.Application.Interfaces.Users;
using FixTrading.Common.ViewModels.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace FixTrading.API.cs.Pages;

public class RegisterModel : PageModel
{
    private readonly IUserAccountService _userAccountService;
    private readonly ILogger<RegisterModel> _logger;

    public RegisterModel(IUserAccountService userAccountService, ILogger<RegisterModel> logger)
    {
        _userAccountService = userAccountService;
        _logger = logger;
    }

    [BindProperty]
    public RegisterInput Input { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public void OnGet()
    {
        ErrorMessage = null;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Input.FullName))
        {
            ErrorMessage = "Ad Soyad alanı boş olamaz.";
            return Page();
        }

        if (string.IsNullOrWhiteSpace(Input.Email))
        {
            ErrorMessage = "Email adresi boş olamaz.";
            return Page();
        }

        if (string.IsNullOrWhiteSpace(Input.Password) || Input.Password.Length < 6)
        {
            ErrorMessage = "Şifre en az 6 karakter olmalı.";
            return Page();
        }

        var normalizedEmail = Input.Email.Trim().ToLowerInvariant();

        bool exists;
        try
        {
            exists = await _userAccountService.IsEmailRegisteredAsync(normalizedEmail, HttpContext.RequestAborted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Register: DB email kontrolü sırasında hata");
            ErrorMessage = "Sunucu hatası oluştu, lütfen tekrar deneyin.";
            return Page();
        }

        if (exists)
        {
            ErrorMessage = "Bu email zaten kayıtlı.";
            return Page();
        }

        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(Input.Password);

        try
        {
            await _userAccountService.RegisterNewUserAsync(
                Input.FullName.Trim(),
                normalizedEmail,
                hashedPassword,
                HttpContext.RequestAborted);
            _logger.LogInformation("Register success. Email={Email}", normalizedEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Register: kayıt sırasında hata");
            ErrorMessage = "Kayıt sırasında hata oluştu: " + ex.Message;
            return Page();
        }

        return RedirectToPage("/Login");
    }
}
