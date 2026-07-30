using CostVision.Domain.Models.MoneyMovements;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CostVision.Infrastructure.DataBase.ModelsConfigure.MoneyMovements
{
    public class MoneyMovementConfigure : IEntityTypeConfiguration<MoneyMovement>
    {
        public void Configure(EntityTypeBuilder<MoneyMovement> builder)
        {
            builder.ToTable("MoneyMovements");

            builder.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasDefaultValueSql("NEWSEQUENTIALID()");

            builder.Property(e => e.AccountId)
                .IsRequired();

            builder.Property(e => e.Amount)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.Property(e => e.Type)
                .IsRequired();

            builder.Property(e => e.OccurredAt)
                .IsRequired();

            builder.Property(e => e.Comment)
                .HasMaxLength(1024);

            builder.Property(e => e.ImportComment)
                .HasMaxLength(1024);

            builder.Property(e => e.CreatedByUserId)
                .IsRequired();

            builder.Property(e => e.PerformedByUserId)
                .IsRequired();

            builder.Property(e => e.CreatedAtUtc)
                .IsRequired();

            builder.Property(e => e.Source)
                .IsRequired();

            builder.HasOne(e => e.Account)
                .WithMany(e => e.MoneyMovements)
                .HasForeignKey(e => e.AccountId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(e => e.CreatedByUser)
                .WithMany(e => e.CreatedMoneyMovements)
                .HasForeignKey(e => e.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(e => e.PerformedByUser)
                .WithMany(e => e.PerformedMoneyMovements)
                .HasForeignKey(e => e.PerformedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(e => new { e.AccountId, e.OccurredAt });
            builder.HasIndex(e => new { e.AccountId, e.OccurredAt, e.Amount, e.Type });
            builder.HasIndex(e => e.CreatedByUserId);
            builder.HasIndex(e => e.PerformedByUserId);

            builder.Navigation(e => e.ReceiptLinks)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }
}
