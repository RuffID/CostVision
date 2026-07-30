using CostVision.Domain.Models.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CostVision.Infrastructure.DataBase.ModelsConfigure.Authorization
{
    public class RoleConfigure : IEntityTypeConfiguration<Role>
    {
        public void Configure(EntityTypeBuilder<Role> builder)
        {
            builder.ToTable("Roles");

            builder.HasKey(e => e.Id);

            builder.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasDefaultValueSql("NEWSEQUENTIALID()");

            builder.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(Role.MAX_NAME_LENGTH);

            builder.HasIndex(e => e.Name)
                .IsUnique();

            builder.HasMany(e => e.UserRoles)
                .WithOne(e => e.Role)
                .HasForeignKey(e => e.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Navigation(e => e.Users)
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.Navigation(e => e.UserRoles)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }
}
