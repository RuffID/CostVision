using CostVision.Domain.Models.MoneyMovements;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CostVision.Infrastructure.DataBase.ModelsConfigure.MoneyMovements
{
    public class MoneyMovementReceiptConfigure : IEntityTypeConfiguration<MoneyMovementReceipt>
    {
        public void Configure(EntityTypeBuilder<MoneyMovementReceipt> builder)
        {
            builder.ToTable("MoneyMovementReceipts");

            builder.HasKey(e => new { e.MoneyMovementId, e.ReceiptId });

            builder.Property(e => e.MoneyMovementId)
                .IsRequired();

            builder.Property(e => e.ReceiptId)
                .IsRequired();

            builder.Property(e => e.CreatedByUserId)
                .IsRequired();

            builder.Property(e => e.CreatedAtUtc)
                .IsRequired();

            builder.HasOne(e => e.MoneyMovement)
                .WithMany(e => e.ReceiptLinks)
                .HasForeignKey(e => e.MoneyMovementId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(e => e.Receipt)
                .WithMany(e => e.MoneyMovementLinks)
                .HasForeignKey(e => e.ReceiptId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(e => e.CreatedByUser)
                .WithMany(e => e.CreatedMoneyMovementReceiptLinks)
                .HasForeignKey(e => e.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(e => e.ReceiptId);
            builder.HasIndex(e => e.CreatedByUserId);
        }
    }
}
