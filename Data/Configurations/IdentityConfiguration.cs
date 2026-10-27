using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RothaShop.Models;
namespace RothaShop.Data.Configurations;
public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(u => u.FullName)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(u => u.AvatarUrl)
            .HasMaxLength(300);

        builder.Property(u => u.CreatedAt)
            .IsRequired();

        builder.HasIndex(u => u.IsActive);
    }
}
