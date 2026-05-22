using FixTrading.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FixTrading.Persistence.Configurations;


// UserEntity sinifinin veritabanindaki karsligini tanimlayan konfigürasyon sinifi. 
// IEntityTypeConfiguration arayuzunu implement ederek, UserEntity'nin veritabanindaki tablo yapisini ve sutun özelliklerini belirler.
public class UserConfiguration : IEntityTypeConfiguration<UserEntity>
{
    // Configure metodu, UserEntity'nin veritabanindaki karsligini tanimlar.
    public void Configure(EntityTypeBuilder<UserEntity> builder)
    {
        builder.ToTable("users");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.FullName)
            .HasColumnName("full_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Email)
            .HasColumnName("email")
            .HasMaxLength(320)
            .IsRequired();

        builder.HasIndex(x => x.Email)
            .IsUnique();

        builder.Property(x => x.Password)
            .HasColumnName("password")
            .IsRequired();

        builder.Property(x => x.Role)
            .HasColumnName("role")
            .HasMaxLength(20)
            .IsRequired();
    }
}

