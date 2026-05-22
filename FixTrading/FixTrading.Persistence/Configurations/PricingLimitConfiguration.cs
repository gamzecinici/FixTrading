using FixTrading.Common.Dtos.Instrument;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FixTrading.Persistence.Entities;

namespace FixTrading.Persistence.Configurations;

// Bu sınıf, PricingLimitEntity'nin veritabanindaki karsiligini tanimlar.
// pricing_limits tablosunun yapisini ve iliskilerini belirtir.
public class PricingLimitConfiguration : IEntityTypeConfiguration<PricingLimitEntity>
{

    // Configure metodu, Entity Framework Core tarafından cagrilir ve PricingLimitEntity'nin veritabanindaki yapisini tanimlar.
    public void Configure(EntityTypeBuilder<PricingLimitEntity> builder)
    {
        builder.ToTable("pricing_limits");

        builder.HasKey(e => e.Id);   // Id property'sinin birincil anahtar oldugunu belirtir.

        builder.Property(e => e.Id)
            .HasColumnName("id");

        builder.Property(e => e.InstrumentId)     // InstrumentId property'sinin veritabanindaki sütun adini ve türünü belirtir.
            .HasColumnName("instrument_id");

        builder.Property(e => e.MinMid)           // numeric(18,8): 8 ondalik basamaga kadar destekler.
            .HasPrecision(18, 8)
            .HasColumnName("min_mid");

        builder.Property(e => e.MaxMid)           // numeric(18,8): 8 ondalik basamaga kadar destekler.
            .HasPrecision(18, 8)
            .HasColumnName("max_mid");

        builder.Property(e => e.MaxSpread)       // numeric(18,8): 8 ondalik basamaga kadar destekler.
            .HasPrecision(18, 8)
            .HasColumnName("max_spread");

        builder.Property(e => e.RecordDate)      // RecordDate property'sinin veritabanindaki sütun adini ve türünü belirtir.
            .HasColumnName("record_date");

        builder.Property(e => e.RecordUser)      // RecordUser property'sinin veritabanindaki sütun adini ve türünü belirtir.
            .HasMaxLength(100)
            .HasColumnName("record_user");

        builder.Property(e => e.RecordCreateDate)    // RecordCreateDate property'sinin veritabanindaki sütun adini ve turunu belirtir.
            .HasColumnName("record_create_date");

        builder.HasOne(e => e.Instrument)            // PricingLimitEntity'nin InstrumentId property'si ile DtoInstrument arasindaki iliskiyi tanimlar.
            .WithMany()
            .HasForeignKey(e => e.InstrumentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
