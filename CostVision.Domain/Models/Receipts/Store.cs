using EFCoreLibrary.Abstractions.Entity;

namespace CostVision.Domain.Models.Receipts
{
    public class Store : IEntity<Guid>
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string NormalizedName { get; set; } = string.Empty;

        public string Address { get; set; } = string.Empty;

        public string NormalizedAddress { get; set; } = string.Empty;

        public string? AdaptiveName { get; set; }

        public ICollection<Receipt> Receipts { get; set; } = new List<Receipt>();

        /// <summary>
        /// Обновляет отображаемые данные магазина.
        /// </summary>
        public void UpdateDetails(string? name, string normalizedName, string? address, string normalizedAddress)
        {
            Name = string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim();
            NormalizedName = normalizedName;
            Address = string.IsNullOrWhiteSpace(address) ? string.Empty : address.Trim();
            NormalizedAddress = normalizedAddress;
        }

        /// <summary>
        /// Обновляет адаптивное название магазина.
        /// </summary>
        public void UpdateAdaptiveName(string? adaptiveName)
        {
            AdaptiveName = string.IsNullOrWhiteSpace(adaptiveName) ? null : adaptiveName.Trim();
        }
    }
}
