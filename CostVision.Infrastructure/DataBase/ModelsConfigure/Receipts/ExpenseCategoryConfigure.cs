using CostVision.Domain.Models.Receipts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CostVision.Infrastructure.DataBase.ModelsConfigure.Receipts
{
    public class ExpenseCategoryConfigure : IEntityTypeConfiguration<ExpenseCategory>
    {
        public void Configure(EntityTypeBuilder<ExpenseCategory> builder)
        {
            builder.ToTable("ExpenseCategories");

            builder.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasDefaultValueSql("NEWSEQUENTIALID()");

            builder.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(ExpenseCategory.MAX_NAME_LENGTH);

            builder.Property(x => x.Description)
                .HasMaxLength(ExpenseCategory.MAX_DESCRIPTION_LENGTH);

            builder.Property(x => x.IsArchived)
                .IsRequired();

            builder.Property(x => x.UserId)
                .IsRequired();

            builder.HasOne(x => x.User)
                .WithMany(u => u.Categories)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.Parent)
                .WithMany(x => x.Children)
                .HasForeignKey(x => x.ParentId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(x => x.ReceiptItems)
                .WithOne(x => x.Category)
                .HasForeignKey(x => x.CategoryId);

            builder.HasIndex(x => new { x.UserId, x.Name })
                .IsUnique();

            builder.Navigation(x => x.User)
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.Navigation(x => x.Parent)
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.Navigation(x => x.Children)
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.Navigation(x => x.ReceiptItems)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }
}
