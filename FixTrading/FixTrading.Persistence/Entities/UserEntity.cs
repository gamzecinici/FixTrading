namespace FixTrading.Persistence.Entities;


// Bu sinif, kullanici bilgilerini temsil eder ve veritabaninda "users" tablosuna karsılık gelir.
public class UserEntity
{
    public int Id { get; set; }
    public string FullName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string Password { get; set; } = null!;
    public string Role { get; set; } = null!;
}

