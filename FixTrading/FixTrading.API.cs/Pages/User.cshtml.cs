using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FixTrading.API.cs.Pages;

[Authorize(Roles = "user,admin")]
public class UserModel : PageModel
{
    public void OnGet()
    {
    }
}

