using CostVision.Models.Receipts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CostVision.DataBase.ModelsConfigure.Receipts
{
    public class ReceiptAccountConfigure : IEntityTypeConfiguration<ReceiptAccount>
    {
        public void Configure(EntityTypeBuilder<ReceiptAccount> builder)
        {
            builder.ToTable("ReceiptAccounts");

            builder.HasKey(e => new { e.ReceiptId, e.AccountId });

            builder.Property(e => e.ReceiptId)
                .IsRequired();

            builder.Property(e => e.AccountId)
                .IsRequired();

            builder.HasOne(e => e.Receipt)
                .WithMany(e => e.Accounts)
                .HasForeignKey(e => e.ReceiptId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(e => e.Account)
                .WithMany(e => e.ReceiptLinks)
                .HasForeignKey(e => e.AccountId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(e => e.AccountId);
        }
    }
}
