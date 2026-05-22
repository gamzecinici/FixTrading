//APİ ile application katmanı arasında veri tasima islemi yapmak icin kullanilan veri tasima modelleridir

namespace FixTrading.Application.Contracts;

//Login sırasında kullanıcıyı bulmak ve şifresini doğrulamak icin kullanilir
public sealed record UserLoginRecord(int Id, string FullName, string Email, string PasswordHash, string Role);

//Kullaniciyi listelemek icin kullanilir, sifre bilgisi icermez
public sealed record UserListRecord(int Id, string FullName, string Email, string Role);
