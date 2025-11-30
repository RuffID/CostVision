using CostVision.Models.Authorization;

namespace CostVision.Models.Receipts
{
    /// <summary>
    /// Категория расходов, применяется к товару
    /// </summary>
    public class ExpenseCategory
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        // Владелец категории (чтобы у каждого юзера были свои категории)
        public Guid UserId { get; set; }
        public User? User { get; set; }

        // Иерархия категорий
        public Guid? ParentId { get; set; }
        public ExpenseCategory? Parent { get; set; }
        public List<ExpenseCategory> Children { get; set; } = new();

        public bool IsArchived { get; set; }

        // Навигация до строк чеков
        public List<ReceiptItem> ReceiptItems { get; set; } = new();
    }
}
