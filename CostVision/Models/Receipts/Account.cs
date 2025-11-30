using CostVision.Interfaces.Entity;
using CostVision.Models.Authorization;

namespace CostVision.Models.Receipts
{
    /// <summary>
    /// Счёт, кошелёк (например "Основная карта", "Наличные", "Совместный бюджет")
    /// </summary>
    public class Account : IEntity<Guid>, ICopyable<Account>
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public bool IsArchived { get; set; }
        public bool IsDefault { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        public Guid CreatedByUserId { get; set; }

        public virtual User? CreatedByUser { get; set; }

        public virtual List<AccountMember> Members { get; set; } = new();

        public virtual List<ReceiptAccount> ReceiptLinks { get; set; } = new();

        public void CopyData(Account entity)
        {
            Name = entity.Name;
            Description = entity.Description;
            IsArchived = entity.IsArchived;
            IsDefault = entity.IsDefault;
        }
    }
}
