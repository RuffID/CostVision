using CostVision.Domain.Models.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CostVision.Infrastructure.DataBase.ModelsConfigure.Authorization
{
    public class UserRoleConfigure : IEntityTypeConfiguration<UserRole>
    {
        public void Configure(EntityTypeBuilder<UserRole> builder)
        {
            builder.ToTable("UserRoles");

            builder.HasKey(e => new { e.UserId, e.RoleId });

            builder.Property(e => e.UserId)
                .IsRequired();

            builder.Property(e => e.RoleId)
                .IsRequired();

            builder.HasOne(e => e.User)
                .WithMany(e => e.UserRoles)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(e => e.Role)
                .WithMany(e => e.UserRoles)
                .HasForeignKey(e => e.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Navigation(e => e.User)
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.Navigation(e => e.Role)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }
}
