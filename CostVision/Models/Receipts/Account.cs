using CostVision.Abstractions.Entity;
using CostVision.Models.Authorization;

namespace CostVision.Models.Receipts
{
    /// <summary>
    /// Счёт, кошелёк (например "Основная карта", "Наличные", "Совместный бюджет")
    /// </summary>
    public class Account : IEntity<Guid>
    {
        public const string DEFAULT_COLOR_HEX = "#0D6EFD";

        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        /// <summary>
        /// Цвет счёта в формате HEX.
        /// </summary>
        public string ColorHex { get; set; } = DEFAULT_COLOR_HEX;

        public bool IsArchived { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        public Guid CreatedByUserId { get; set; }

        public virtual User? CreatedByUser { get; set; }

        public virtual List<AccountMember> Members { get; set; } = new();

        public virtual List<ReceiptAccount> ReceiptLinks { get; set; } = new();
    }
}
