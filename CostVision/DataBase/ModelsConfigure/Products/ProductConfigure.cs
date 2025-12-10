using CostVision.Models.Receipts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CostVision.DataBase.ModelsConfigure.Products
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

            builder.Property(x => x.ProductCode)
                .HasMaxLength(150);

            builder.Property(x => x.NormalizedName)
                .HasMaxLength(500);

            builder.HasIndex(p => p.NormalizedName)
                .IsUnique();
        }
    }
}
