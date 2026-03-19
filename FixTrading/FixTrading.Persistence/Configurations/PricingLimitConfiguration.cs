using FixTrading.Common.Dtos.Instrument;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FixTrading.Persistence.Entities;

namespace FixTrading.Persistence.Configurations;

// Bu sınıf, PricingLimitEntity'nin veritabanındaki karşılığını tanımlar.
// pricing_limits tablosunun yapısını ve ilişkilerini belirtir.
public class PricingLimitConfiguration : IEntityTypeConfiguration<PricingLimitEntity>
{

    // Configure metodu, Entity Framework Core tarafından çağrılır ve PricingLimitEntity'nin veritabanındaki yapısını tanımlar.
    public void Configure(EntityTypeBuilder<PricingLimitEntity> builder)
    {
        builder.ToTable("pricing_limits");

        builder.HasKey(e => e.Id);   // Id property'sinin birincil anahtar olduğunu belirtir.

        builder.Property(e => e.InstrumentId)     // InstrumentId property'sinin veritabanındaki sütun adını ve türünü belirtir.
            .HasColumnName("instrument_id");

        builder.Property(e => e.MinMid)           // MinMid property'sinin veritabanındaki sütun adını ve türünü belirtir.
            .HasPrecision(18, 8)
            .HasColumnName("min_mid");

        builder.Property(e => e.MaxMid)           // MaxMid property'sinin veritabanındaki sütun adını ve türünü belirtir.
            .HasPrecision(18, 8)
            .HasColumnName("max_mid");

        builder.Property(e => e.MaxSpread)       // MaxSpread property'sinin veritabanındaki sütun adını ve türünü belirtir.
            .HasPrecision(18, 8)
            .HasColumnName("max_spread");

        builder.Property(e => e.RecordDate)      // RecordDate property'sinin veritabanındaki sütun adını ve türünü belirtir.
            .HasColumnName("record_date");

        builder.Property(e => e.RecordUser)      // RecordUser property'sinin veritabanındaki sütun adını ve türünü belirtir.
            .HasMaxLength(100)
            .HasColumnName("record_user");

        builder.Property(e => e.RecordCreateDate)    // RecordCreateDate property'sinin veritabanındaki sütun adını ve türünü belirtir.
            .HasColumnName("record_create_date");

        builder.HasOne(e => e.Instrument)            // PricingLimitEntity'nin InstrumentId property'si ile DtoInstrument arasındaki ilişkiyi tanımlar.
            .WithMany()
            .HasForeignKey(e => e.InstrumentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
