using Microsoft.EntityFrameworkCore;
using FixTrading.Common.Dtos.Instrument;
using FixTrading.Common.Dtos.Trade;
using FixTrading.Persistence.Entities;

namespace FixTrading.Persistence;

//bu sınıf, Entity Framework Core kullanarak veritabanı işlemlerini yönetir.
//AppDbContext, DbContext sınıfından türetilmiştir ve veritabanı bağlantısı ve tabloların yapılandırılması için gerekli ayarları içerir.
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)   //DbContextOptions, veritabanı bağlantısı ve diğer yapılandırma seçeneklerini içeren bir sınıftır.
        : base(options)
    {
    }

    //DbSet, Entity Framework Core'da bir tabloyu temsil eder. DtoInstrument ve DtoTrade sınıfları, veritabanındaki tabloların yapısını tanımlar.
    public DbSet<DtoInstrument> Instruments { get; set; } = null!;

    //DbSet, Entity Framework Core'da bir tabloyu temsil eder. DtoTrade sınıfı, veritabanındaki Trade tablosunun yapısını tanımlar.
    public DbSet<DtoTrade> Trades { get; set; } = null!;

    //PricingLimitEntity, veritabanındaki PricingLimits tablosunun yapısını tanımlar. Bu tablo, fiyatlandırma limitlerini saklamak için kullanılır.  
    public DbSet<PricingLimitEntity> PricingLimits { get; set; } = null!;

    //UserEntity, veritabanındaki Users tablosunun yapısını tanımlar. Bu tablo, kullanıcı bilgilerini saklamak için kullanılır.
    public DbSet<UserEntity> Users { get; set; } = null!;

    public DbSet<SystemParameterEntity> SystemParameters { get; set; } = null!;

    //OnModelCreating metodu, Entity Framework Core tarafından veritabanı modeli oluşturulurken çağrılır.
    //Bu metodun içinde, model yapılandırmalarını uygulamak için ApplyConfigurationsFromAssembly kullanılır.
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // IEntityTypeConfiguration sınıfları (örn. PricingLimitConfiguration) uygulanır
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
