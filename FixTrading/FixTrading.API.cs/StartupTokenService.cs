namespace FixTrading.API;

/// <summary>
/// Uygulama her başlatıldığında benzersiz bir token üretir.
/// Login sırasında bu token bir claim olarak cookie'ye yazılır.
/// Her istekte cookie'deki token, mevcut token ile karşılaştırılır;
/// eşleşmiyorsa (yani uygulama yeniden başlatılmışsa) oturum reddedilir
/// ve kullanıcı login ekranına yönlendirilir.
/// </summary>
public sealed class StartupTokenService
{
    public string Token { get; } = Guid.NewGuid().ToString("N");
}
