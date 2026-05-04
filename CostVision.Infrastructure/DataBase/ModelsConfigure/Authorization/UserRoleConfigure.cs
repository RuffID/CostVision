using CostVision.Domain.Models.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CostVision.Infrastructure.DataBase.ModelsConfigure.Authorization
{
    public class UserRoleConfigure : IEntityTypeConfiguration<UserRole>
    {
        public void Configure(EntityTypeBuilder<UserRole> builder)
        {
            // Задавать имя таблицы
            builder.ToTable("UserRoles");

            // Составной ключ (UserId + RoleId)
            builder.HasKey(e => new { e.UserId, e.RoleId });

            builder.Property(e => e.UserId)
                .IsRequired();

            builder.Property(e => e.RoleId)
                .IsRequired();

            // Связь с пользователем
            builder.HasOne(e => e.User)
                .WithMany(e => e.UserRoles)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Связь с ролью
            builder.HasOne(e => e.Role)
                .WithMany(e => e.UserRoles)
                .HasForeignKey(e => e.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
