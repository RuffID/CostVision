using EFCoreLibrary.Abstractions.Entity;

namespace CostVision.Domain.Models.Receipts
{
    public class Product : IEntity<Guid>
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string NormalizedName { get; set; } = string.Empty;

        public string? AdaptiveName { get; set; }

        public ICollection<ReceiptItem> ReceiptItems { get; set; } = new List<ReceiptItem>();

        /// <summary>
        /// Обновляет отображаемые данные товара.
        /// </summary>
        public void UpdateDetails(string name, string normalizedName)
        {
            Name = name;
            NormalizedName = normalizedName;
        }

        /// <summary>
        /// Обновляет адаптивное название товара.
        /// </summary>
        public void UpdateAdaptiveName(string? adaptiveName)
        {
            AdaptiveName = string.IsNullOrWhiteSpace(adaptiveName) ? null : adaptiveName.Trim();
        }
    }
}
