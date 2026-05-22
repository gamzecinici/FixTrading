using FixTrading.Application.Contracts;
using FixTrading.Application.Interfaces.Users;
using FixTrading.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace FixTrading.Persistence.Services;
//Kullanici hesaplariyla ilgili islemleri yoneten servistir,
//bu servis veri ceker ve guncelleme islemleri yapar, kullanici kaydi, login, rol degistirme gibi islemleri icerir
public sealed class UserAccountService : IUserAccountService
{
    private readonly AppDbContext _db;

    //Kullanici islemlerini tek bir merkezde toplar ve katmanlar arasi bağgimliligi azaltir
    public UserAccountService(AppDbContext db)
    {
        _db = db;
    }
    //Giris islemi icin kullaniciyi maile gore bulur, eger kayit yoksa null doner
    public async Task<UserLoginRecord?> GetUserForLoginByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default)
    {
        var row = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail, cancellationToken);
        return row is null
            ? null
            : new UserLoginRecord(row.Id, row.FullName, row.Email, row.Password, row.Role);
    }

    //Kullanici kaydi yaparken emailin zaten kayitli olup olmadigini kontrol eder, eger kayit varsa true doner
    public Task<bool> IsEmailRegisteredAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
        _db.Users.AnyAsync(u => u.Email.ToLower() == normalizedEmail, cancellationToken);

    //Yeni kullanici kaydi yapar, eger mail zaten kayitli ise hata firlatir
    public async Task RegisterNewUserAsync(string fullName, string normalizedEmail, string passwordBcryptHash, CancellationToken cancellationToken = default)
    {
        _db.Users.Add(new UserEntity
        {
            FullName = fullName.Trim(),
            Email = normalizedEmail,
            Password = passwordBcryptHash,
            Role = "user"
        });
        await _db.SaveChangesAsync(cancellationToken);
    }

    //Tum kullanicilari isimlerine gore siralayarak listeler, admin panelinde kullanici yonetimi icin kullanilir
    public async Task<IReadOnlyList<UserListRecord>> ListUsersOrderedByNameAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _db.Users
            .AsNoTracking()
            .OrderBy(x => x.FullName)
            .Select(x => new UserListRecord(x.Id, x.FullName, x.Email, x.Role))
            .ToListAsync(cancellationToken);
        return rows;
    }

    //Admin panelinde yeni kullanici eklemek icin kullanilir, eger mail zaten kayitli ise hata firlatir
    public async Task AddUserAsync(string fullName, string normalizedEmail, string passwordBcryptHash, string role, CancellationToken cancellationToken = default)
    {
        _db.Users.Add(new UserEntity
        {
            FullName = fullName.Trim(),
            Email = normalizedEmail,
            Password = passwordBcryptHash,
            Role = role
        });
        await _db.SaveChangesAsync(cancellationToken);
    }

    //Admin panelinde kullanici silmek icin kullanilir, eger kullanici bulunamazsa islem yapmaz
    public async Task DeleteUserAsync(int id, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user is null)
            return;
        _db.Users.Remove(user);
        await _db.SaveChangesAsync(cancellationToken);
    }

    //Admin panelinde kullanici rolunu degistirmek icin kullanilir, eger kullanici bulunamazsa islem yapmaz, admin ise user yapar, user ise admin yapar
    public async Task ToggleUserRoleAsync(int id, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user is null)
            return;
        user.Role = string.Equals(user.Role, "admin", StringComparison.OrdinalIgnoreCase) ? "user" : "admin";
        await _db.SaveChangesAsync(cancellationToken);
    }
}
