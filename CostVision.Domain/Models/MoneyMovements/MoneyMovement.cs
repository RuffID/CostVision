using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.Enums.MoneyMovements;
using CostVision.Domain.Models.Receipts;
using EFCoreLibrary.Abstractions.Entity;

namespace CostVision.Domain.Models.MoneyMovements
{
    /// <summary>
    /// Операция движения денег по счёту.
    /// </summary>
    public class MoneyMovement : IEntity<Guid>
    {
        public Guid Id { get; set; }

        public Guid AccountId { get; set; }

        public Account Account { get; set; } = null!;

        public decimal Amount { get; set; }

        public MoneyMovementType Type { get; set; }

        public DateTime OccurredAt { get; set; }

        public string? Comment { get; set; }

        public string? ImportComment { get; set; }

        public Guid CreatedByUserId { get; set; }

        public User? CreatedByUser { get; set; }

        public Guid PerformedByUserId { get; set; }

        public User? PerformedByUser { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        public DateTime? UpdatedAtUtc { get; set; }

        public MoneyMovementSource Source { get; set; }

        /// <summary>
        /// Обновляет время последнего изменения операции.
        /// </summary>
        public void MarkUpdated(DateTime updatedAtUtc)
        {
            UpdatedAtUtc = updatedAtUtc;
        }
    }
}
