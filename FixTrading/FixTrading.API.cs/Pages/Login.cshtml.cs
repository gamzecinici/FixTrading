using System.Security.Claims;
using FixTrading.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FixTrading.API.cs.Pages;

[IgnoreAntiforgeryToken]
public class LoginModel : PageModel
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<LoginModel> _logger;

    public LoginModel(AppDbContext dbContext, ILogger<LoginModel> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [BindProperty]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    public string Password { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        ErrorMessage = null;

        if (User.Identity?.IsAuthenticated == true)
        {
            var existingRole = User.FindFirstValue(ClaimTypes.Role);
            return existingRole == "admin" ? Redirect("/Admin") : Redirect("/User");
        }

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Email ve şifre giriniz.";
            return Page();
        }

        var normalizedEmail = Email.Trim().ToLowerInvariant();

        // DB'den kullanıcıyı getir — C# tarafında email karşılaştırması
        var users = await _dbContext.Users
            .AsNoTracking()
            .ToListAsync();

        var user = users.FirstOrDefault(x =>
            string.Equals(x.Email.Trim(), normalizedEmail, StringComparison.OrdinalIgnoreCase));

        if (user is null)
        {
            _logger.LogWarning("Login: kullanıcı bulunamadı. Email={Email}", normalizedEmail);
            ErrorMessage = "Email veya şifre hatalı.";
            return Page();
        }

        bool passwordOk;
        try
        {
            passwordOk = BCrypt.Net.BCrypt.Verify(Password, user.Password);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login: BCrypt.Verify sırasında hata. UserId={Id}", user.Id);
            ErrorMessage = "Şifre doğrulaması sırasında hata oluştu.";
            return Page();
        }

        if (!passwordOk)
        {
            _logger.LogWarning("Login: şifre yanlış. Email={Email}, UserId={Id}", normalizedEmail, user.Id);
            ErrorMessage = "Email veya şifre hatalı.";
            return Page();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = false });

        _logger.LogInformation("Login: başarılı. Email={Email}, Role={Role}", normalizedEmail, user.Role);

        return user.Role == "admin" ? Redirect("/Admin") : Redirect("/User");
    }
}
