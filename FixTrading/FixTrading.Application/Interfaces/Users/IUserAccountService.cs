//Kullanici hesaplariyla ilgili servis arayuzu. Kullanici kaydi, login, rol degistirme gibi islemleri icerir.
using FixTrading.Application.Contracts;

namespace FixTrading.Application.Interfaces.Users;


//Kullanici islemlerini tek bir merkezde toplar ve katmanlar arasi bağgimliligi azaltir
public interface IUserAccountService
{
    //Giris islemi icin kullaniciyi maile gore bulur 
    Task<UserLoginRecord?> GetUserForLoginByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default);

    //Yeni kullanici kaydi icin maile gore kontrol yapar, eger kayit varsa true doner
    Task<bool> IsEmailRegisteredAsync(string normalizedEmail, CancellationToken cancellationToken = default);

    //Yeni kullanici kaydi yapar, eger mail zaten kayitli ise hata firlatir
    Task RegisterNewUserAsync(string fullName, string normalizedEmail, string passwordBcryptHash, CancellationToken cancellationToken = default);

    //Tum kullanicilari isimlerine gore siralayarak listeler, sifre bilgisi icermez
    Task<IReadOnlyList<UserListRecord>> ListUsersOrderedByNameAsync(CancellationToken cancellationToken = default);

    //Yeni kullanici kaydi yapar, eger mail zaten kayitli ise hata firlatir, rol bilgisi de ekler
    Task AddUserAsync(string fullName, string normalizedEmail, string passwordBcryptHash, string role, CancellationToken cancellationToken = default);

    //Kullanici kaydini siler, eger id'ye sahip bir kullanici yoksa hata firlatir
    Task DeleteUserAsync(int id, CancellationToken cancellationToken = default);

    //Admin panelinden kullanici rolunu degistirmek icin kullanilir, eger id'ye sahip bir kullanici yoksa hata firlatir
    Task ToggleUserRoleAsync(int id, CancellationToken cancellationToken = default);
}
