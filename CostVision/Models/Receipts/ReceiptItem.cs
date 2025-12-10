using CostVision.Interfaces.Entity;
using CostVision.Models.Enums.Document;

namespace CostVision.Models.Receipts
{
    public class ReceiptItem : IEntity<Guid>, ICopyable<ReceiptItem>
    {
        public Guid Id { get; set; }
        public decimal Price { get; set; }
        public decimal Quantity { get; set; }
        public decimal Sum { get; set; }
        public int Nds { get; set; }

        public PaymentType PaymentType { get; set; }
        public ProductType ProductType { get; set; }
        public QuantityMeasureType ItemsQuantityMeasure { get; set; }

        public Guid ReceiptId { get; set; }
        public Guid? ProductId { get; set; }
        public Guid? CategoryId { get; set; }
        public virtual Receipt? Receipt { get; set; }
        public virtual ExpenseCategory? Category { get; set; }
        public virtual Product? Product { get; set; }

        public void CopyData(ReceiptItem entity)
        {
            Price = entity.Price;
            Quantity = entity.Quantity;
            Sum = entity.Sum;
            Nds = entity.Nds;
            PaymentType = entity.PaymentType;
            ProductType = entity.ProductType;
            ItemsQuantityMeasure = entity.ItemsQuantityMeasure;
        }
    }
}
