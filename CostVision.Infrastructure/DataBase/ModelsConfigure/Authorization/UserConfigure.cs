using CostVision.Domain.Models.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CostVision.Infrastructure.DataBase.ModelsConfigure.Authorization
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
                .HasMaxLength(User.MAX_LOGIN_LENGTH);

            builder.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(User.MAX_NAME_LENGTH);

            builder.Property(x => x.PasswordHash)
                .IsRequired()
                .HasMaxLength(User.PASSWORD_HASH_MAX_LENGTH);

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

            builder.Navigation(e => e.Roles)
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.Navigation(e => e.UserRoles)
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.HasMany(x => x.CreatedReceipts)
                .WithOne(x => x.CreatedByUser)
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Navigation(x => x.CreatedReceipts)
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.HasMany(x => x.AccountMemberships)
                .WithOne(x => x.User)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Navigation(x => x.AccountMemberships)
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.HasMany(e => e.Categories)
                .WithOne(e => e.User)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Navigation(e => e.Categories)
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.HasMany(e => e.Accounts)
                .WithOne(e => e.CreatedByUser)
                .HasForeignKey(e => e.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Navigation(e => e.Accounts)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }
}
