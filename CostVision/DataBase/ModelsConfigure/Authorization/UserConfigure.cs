using CostVision.Models.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CostVision.DataBase.ModelsConfigure.Authorization
{
    public class UserConfigure : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.ToTable("Users");

            builder.HasKey(x => x.Id);

            builder.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasDefaultValueSql("NEWSEQUENTIALID()");

            builder.Property(x => x.Login)
                .IsRequired()
                .HasMaxLength(128);

            builder.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(256);

            builder.Property(x => x.PasswordHash)
                .IsRequired()
                .HasMaxLength(512);

            builder.Property(x => x.IsActive)
                .IsRequired();

            builder.Property(x => x.CreatedAtUtc)
                .IsRequired();

            builder.Property(x => x.LastLoginAtUtc);

            builder.HasIndex(x => x.Login)
                .IsUnique();

            builder.HasMany(e => e.Roles)
                .WithMany(e => e.Users)
                .UsingEntity<UserRole>();

            builder.HasMany(x => x.CreatedReceipts)
                .WithOne(x => x.CreatedByUser)
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(x => x.AccountMemberships)
                .WithOne(x => x.User)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(e => e.Categories)
                .WithOne(e => e.User)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(e => e.Accounts)
                .WithOne(e => e.CreatedByUser)
                .HasForeignKey(e => e.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
