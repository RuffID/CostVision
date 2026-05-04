using CostVision.Domain.Models.Authorization;

namespace CostVision.Domain.Models.Receipts
{
    /// <summary>
    /// Категория расходов, которая может быть назначена позиции чека.
    /// </summary>
    public class ExpenseCategory
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public Guid UserId { get; set; }

        public User? User { get; set; }

        public Guid? ParentId { get; set; }

        public ExpenseCategory? Parent { get; set; }

        public List<ExpenseCategory> Children { get; set; } = new();

        public bool IsArchived { get; set; }

        public List<ReceiptItem> ReceiptItems { get; set; } = new();

        /// <summary>
        /// Архивирует категорию расходов.
        /// </summary>
        public void Archive()
        {
            IsArchived = true;
        }

        /// <summary>
        /// Возвращает категорию расходов из архива.
        /// </summary>
        public void Restore()
        {
            IsArchived = false;
        }
    }
}
