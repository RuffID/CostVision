using CostVision.Domain.Models.Enums.Receipts;

namespace CostVision.Domain.Models.Receipts
{
    public class ReceiptItem
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

        public Receipt? Receipt { get; set; }

        public ExpenseCategory? Category { get; set; }

        public Product? Product { get; set; }
    }
}
