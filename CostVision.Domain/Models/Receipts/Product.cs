using EFCoreLibrary.Abstractions.Entity;

namespace CostVision.Domain.Models.Receipts
{
    public class Product : IEntity<Guid>
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string NormalizedName { get; set; } = string.Empty;

        public string? ProductCode { get; set; }

        public ICollection<ReceiptItem> ReceiptItems { get; set; } = new List<ReceiptItem>();

        /// <summary>
        /// Обновляет отображаемые данные товара.
        /// </summary>
        public void UpdateDetails(string name, string normalizedName, string? productCode)
        {
            Name = name;
            NormalizedName = normalizedName;
            ProductCode = productCode;
        }
    }
}
