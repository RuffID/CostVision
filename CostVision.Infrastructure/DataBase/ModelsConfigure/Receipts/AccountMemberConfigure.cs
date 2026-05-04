using CostVision.Domain.Models.Receipts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CostVision.Infrastructure.DataBase.ModelsConfigure.Receipts
{
    public class AccountMemberConfigure : IEntityTypeConfiguration<AccountMember>
    {
        public void Configure(EntityTypeBuilder<AccountMember> builder)
        {
            builder.ToTable("AccountMembers");

            builder.HasKey(x => new { x.UserId, x.AccountId });

            builder.Property(x => x.Role)
                .IsRequired();

            builder.HasOne(x => x.User)
                .WithMany(x => x.AccountMemberships)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.Account)
                .WithMany(x => x.Members)
                .HasForeignKey(x => x.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
