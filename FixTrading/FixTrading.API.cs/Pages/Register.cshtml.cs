using System.ComponentModel.DataAnnotations;
using FixTrading.Persistence;
using FixTrading.Persistence.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FixTrading.API.cs.Pages;

[IgnoreAntiforgeryToken]
public class RegisterModel : PageModel
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<RegisterModel> _logger;

    public RegisterModel(AppDbContext dbContext, ILogger<RegisterModel> logger)
    {
        _dbContext = dbContext;
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
            exists = await _dbContext.Users.AnyAsync(x => x.Email == normalizedEmail);
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

        var user = new UserEntity
        {
            FullName = Input.FullName.Trim(),
            Email = normalizedEmail,
            Password = hashedPassword,
            Role = "user"
        };

        try
        {
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();
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

    public class RegisterInput
    {
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
