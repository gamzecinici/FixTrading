using Microsoft.EntityFrameworkCore;
using FixTrading.Persistence.Entities;
using FixTrading.Common.Dtos.FixSymbol;

namespace FixTrading.Persistence;

/// <summary>
/// EF Core veritabanı bağlamı. Sadece Persistence katmanında kullanılır.
/// Tüm entity'ler ve tablo eşlemeleri burada tanımlıdır.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    // Enstrüman tablosu
    public DbSet<InstrumentEntity> Instruments { get; set; } = null!;

    // İşlem (trade) tablosu
    public DbSet<TradeEntity> Trades { get; set; } = null!;

    // FixSymbol verisi tablosu (DtoFixSymbol doğrudan entity olarak kullanılır)
    public DbSet<DtoFixSymbol> FixSymbols { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Assembly içindeki tüm IEntityTypeConfiguration sınıflarını uygula
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
