using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.MoneyMovements;
using EFCoreLibrary.Abstractions.Entity;

namespace CostVision.Domain.Models.Receipts
{
    /// <summary>
    /// Счёт, кошелёк или общий бюджет для группировки чеков и расходов.
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

        public User? CreatedByUser { get; set; }

        public List<AccountMember> Members { get; set; } = new();

        public List<ReceiptAccount> ReceiptLinks { get; set; } = new();

        public List<MoneyMovement> MoneyMovements { get; set; } = new();

        /// <summary>
        /// Архивирует счёт.
        /// </summary>
        public void Archive()
        {
            IsArchived = true;
        }

        /// <summary>
        /// Возвращает счёт из архива.
        /// </summary>
        public void Restore()
        {
            IsArchived = false;
        }

        /// <summary>
        /// Проверяет, состоит ли пользователь в участниках счёта.
        /// </summary>
        public bool HasMember(Guid userId)
        {
            return Members.Any(member => member.UserId == userId);
        }
    }
}
