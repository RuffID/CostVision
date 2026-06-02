using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CostVision.Domain.Models.Receipts;

namespace CostVision.Infrastructure.DataBase.ModelsConfigure.Receipts
{
    public class ReceiptConfigure : IEntityTypeConfiguration<Receipt>
    {
        public void Configure(EntityTypeBuilder<Receipt> builder)
        {
            builder.ToTable("Receipts");

            builder.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasDefaultValueSql("NEWSEQUENTIALID()");

            builder.Property(x => x.FiscalDriveNumber)
                .HasMaxLength(32);

            builder.Property(x => x.FiscalDocumentNumber)
                .HasMaxLength(32);

            builder.Property(x => x.FiscalSign)
                .HasMaxLength(32);

            builder.Property(x => x.User)
                .HasMaxLength(256);

            builder.Property(x => x.UserInn)
                .HasMaxLength(32);

            builder.Property(x => x.Region)
                .HasMaxLength(32);

            builder.Property(x => x.CheckNumber)
                .HasMaxLength(32);           

            builder.Property(x => x.TotalSum)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.Property(x => x.CashTotalSum)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.Property(x => x.EcashTotalSum)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.Property(x => x.Nds18)
                .HasPrecision(18, 2);

            builder.Property(x => x.Nds10)
                .HasPrecision(18, 2);

            builder.Property(x => x.Nds0)
                .HasPrecision(18, 2);

            builder.Property(x => x.NdsNo)
                .HasPrecision(18, 2);

            builder.Property(x => x.KktRegId)
                .HasMaxLength(64);

            builder.Property(x => x.NumberKkt)
                .HasMaxLength(64);

            builder.HasOne(x => x.CreatedByUser)
                .WithMany(x => x.CreatedReceipts)
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.Store)
                .WithMany(x => x.Receipts)
                .HasForeignKey(x => x.StoreId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(x => x.Items)
                .WithOne(x => x.Receipt)
                .HasForeignKey(x => x.ReceiptId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(x => x.Accounts)
                .WithOne(x => x.Receipt)
                .HasForeignKey(x => x.ReceiptId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(x => new { x.FiscalDriveNumber, x.FiscalDocumentNumber, x.FiscalSign });

            builder.HasIndex(x => x.StoreId);
        }
    }
}
