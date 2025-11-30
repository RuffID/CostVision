using CostVision.Interfaces.Entity;
using CostVision.Models.Receipts;

namespace CostVision.Models.Products
{
    public class Product : IEntity<Guid>, ICopyable<Product>
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string NormalizedName { get; set; } = string.Empty;
        public string? ProductCode { get; set; }

        public ICollection<ReceiptItem> ReceiptItems { get; set; } = new List<ReceiptItem>();

        public void CopyData(Product entity)
        {
            Name = entity.Name;
            ProductCode = entity.ProductCode;
            NormalizedName = entity.NormalizedName;
        }
    }
}
