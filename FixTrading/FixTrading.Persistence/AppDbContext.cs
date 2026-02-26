using Microsoft.EntityFrameworkCore;
using FixTrading.Common.Dtos.Instrument;
using FixTrading.Common.Dtos.Trade;

namespace FixTrading.Persistence;

/// <summary>
/// EF Core veritabanı bağlamı. Sadece Persistence katmanında kullanılır.
/// Entity kullanılmaz; tüm tablolar DTO yapıları ile eşlenir.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    /// <summary>Enstrüman tablosu. FIX market data akışı bu tablo üzerinden yönetilir.</summary>
    public DbSet<DtoInstrument> Instruments { get; set; } = null!;

    /// <summary>İşlem (trade) tablosu.</summary>
    public DbSet<DtoTrade> Trades { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
