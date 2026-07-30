using CostVision.Domain.Models.Receipts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CostVision.Infrastructure.DataBase.ModelsConfigure.Receipts
{
    public class StoreConfigure : IEntityTypeConfiguration<Store>
    {
        public void Configure(EntityTypeBuilder<Store> builder)
        {
            builder.ToTable("Stores");

            builder.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasDefaultValueSql("NEWSEQUENTIALID()");

            builder.Property(x => x.Name)
                .HasMaxLength(256);

            builder.Property(x => x.NormalizedName)
                .HasMaxLength(256);

            builder.Property(x => x.Address)
                .HasMaxLength(512);

            builder.Property(x => x.NormalizedAddress)
                .HasMaxLength(512);

            builder.Property(x => x.AdaptiveName)
                .HasMaxLength(500);

            builder.HasIndex(x => new { x.NormalizedName, x.NormalizedAddress })
                .IsUnique();

            builder.Navigation(x => x.Receipts)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }
}
