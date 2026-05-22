using System.Security.Claims;

namespace FixTrading.API;

// Audit loglari icin kullanilan kullanici bilgilerini almak icin yardimci sinif
public static class CurrentUserAudit
{
    public static string? GetDisplayNameForAudit(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
            return null;

        var name = user.FindFirstValue(ClaimTypes.Name)?.Trim();
        if (!string.IsNullOrEmpty(name))
            return Truncate(name, 100);

        var email = user.FindFirstValue(ClaimTypes.Email)?.Trim();
        if (!string.IsNullOrEmpty(email))
            return Truncate(email, 100);

        var identityName = user.Identity?.Name?.Trim();
        return string.IsNullOrEmpty(identityName) ? null : Truncate(identityName, 100);
    }

    private static string Truncate(string s, int maxLen) =>
        s.Length <= maxLen ? s : s[..maxLen];
}
