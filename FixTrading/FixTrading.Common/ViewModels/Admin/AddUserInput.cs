namespace FixTrading.Common.ViewModels.Admin;

//Yeni bir kullanici eklemek icin kullanilan ViewModel.
public class AddUserInput
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Role { get; set; } = "user";
}
