namespace FixTrading.Common.ViewModels.Auth;

//Yeni kullanici kaydi icin kullanilan ViewModel.
public class RegisterInput
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
