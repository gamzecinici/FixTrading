using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FixTrading.API.cs.Pages;

[Authorize(Roles = "admin")]
public class AdminModel : PageModel
{
    public void OnGet()
    {
    }
}

