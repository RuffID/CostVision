using CostVision.Domain.Models.Receipts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CostVision.Infrastructure.DataBase.ModelsConfigure.Receipts
{
    public class ProductConfigure : IEntityTypeConfiguration<Product>
    {
        public void Configure(EntityTypeBuilder<Product> builder)
        {
            builder.ToTable("Products");

            builder.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasDefaultValueSql("NEWSEQUENTIALID()");

            builder.Property(x => x.Name)
                .HasMaxLength(500);

            builder.Property(x => x.AdaptiveName)
                .HasMaxLength(500);

            builder.Property(x => x.NormalizedName)
                .HasMaxLength(500);

            builder.HasIndex(p => p.NormalizedName)
                .IsUnique();

            builder.HasIndex(p => p.AdaptiveName)
                .IsUnique()
                .HasFilter("[AdaptiveName] IS NOT NULL");

            builder.Navigation(p => p.ReceiptItems)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }
}
