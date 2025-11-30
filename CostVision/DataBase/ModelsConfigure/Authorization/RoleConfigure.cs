using CostVision.Models.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CostVision.DataBase.ModelsConfigure.Authorization
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
                .HasMaxLength(128);

            builder.HasIndex(e => e.Name)
                .IsUnique();

            builder.HasMany(e => e.UserRoles)
                .WithOne(e => e.Role)
                .HasForeignKey(e => e.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}