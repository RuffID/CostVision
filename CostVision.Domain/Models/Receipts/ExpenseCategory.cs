using CostVision.Domain.Models.Authorization;
using System.Collections.ObjectModel;

namespace CostVision.Domain.Models.Receipts
{
    /// <summary>
    /// Категория расходов, которая может быть назначена позиции чека.
    /// </summary>
    public class ExpenseCategory
    {
        public const int MAX_NAME_LENGTH = 128;
        public const int MAX_DESCRIPTION_LENGTH = 512;

        private User? _user;
        private ExpenseCategory? _parent;
        private readonly List<ExpenseCategory> _children = new();
        private readonly ReadOnlyCollection<ExpenseCategory> _childrenView;
        private readonly List<ReceiptItem> _receiptItems = new();
        private readonly ReadOnlyCollection<ReceiptItem> _receiptItemsView;

        private ExpenseCategory()
        {
            _childrenView = _children.AsReadOnly();
            _receiptItemsView = _receiptItems.AsReadOnly();
        }

        public Guid Id { get; set; }

        public string Name { get; private set; } = string.Empty;

        public string? Description { get; private set; }

        public Guid UserId { get; private set; }

        public User? User => _user;

        public Guid? ParentId { get; private set; }

        public ExpenseCategory? Parent => _parent;

        public IReadOnlyCollection<ExpenseCategory> Children => _childrenView;

        public bool IsArchived { get; private set; }

        public IReadOnlyCollection<ReceiptItem> ReceiptItems => _receiptItemsView;

        /// <summary>
        /// Создаёт категорию расходов с допустимыми начальными данными.
        /// </summary>
        public static bool TryCreate(
            string name,
            string? description,
            Guid userId,
            ExpenseCategory? parent,
            out ExpenseCategory? category,
            out string? error)
        {
            category = null;

            if (userId == Guid.Empty)
            {
                error = "Некорректный идентификатор владельца категории.";
                return false;
            }

            if (parent != null && parent.UserId != userId)
            {
                error = "Родительская категория принадлежит другому пользователю.";
                return false;
            }

            if (!TryNormalizeDetails(name, description, out string normalizedName, out string? normalizedDescription, out error))
                return false;

            category = new ExpenseCategory
            {
                Name = normalizedName,
                Description = normalizedDescription,
                UserId = userId,
                ParentId = parent?.Id == Guid.Empty ? null : parent?.Id,
                _parent = parent
            };
            parent?._children.Add(category);
            return true;
        }

        /// <summary>
        /// Создаёт категорию расходов для указанного пользователя.
        /// </summary>
        public static bool TryCreate(
            string name,
            string? description,
            User user,
            ExpenseCategory? parent,
            out ExpenseCategory? category,
            out string? error)
        {
            category = null;

            if (user == null)
            {
                error = "Владелец категории не указан.";
                return false;
            }

            if (!TryCreate(name, description, user.Id, parent, out category, out error))
                return false;

            category!._user = user;
            return true;
        }

        /// <summary>
        /// Изменяет название и описание категории.
        /// </summary>
        public bool TryUpdateDetails(string name, string? description, out string? error)
        {
            if (!TryNormalizeDetails(name, description, out string normalizedName, out string? normalizedDescription, out error))
                return false;

            Name = normalizedName;
            Description = normalizedDescription;
            return true;
        }

        /// <summary>
        /// Перемещает категорию в другую ветвь дерева.
        /// </summary>
        public bool TryMoveTo(ExpenseCategory? parent, out string? error)
        {
            if (parent != null && parent.UserId != UserId)
            {
                error = "Родительская категория принадлежит другому пользователю.";
                return false;
            }

            if (ReferenceEquals(parent, this) || IsDescendantOf(parent))
            {
                error = "Категория не может быть вложена сама в себя или в свою дочернюю категорию.";
                return false;
            }

            if (ReferenceEquals(_parent, parent))
            {
                error = null;
                return true;
            }

            _parent?._children.Remove(this);
            parent?._children.Add(this);
            _parent = parent;
            ParentId = parent?.Id == Guid.Empty ? null : parent?.Id;
            error = null;
            return true;
        }

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

        private bool IsDescendantOf(ExpenseCategory? category)
        {
            ExpenseCategory? current = category;
            while (current != null)
            {
                if (ReferenceEquals(current, this))
                    return true;

                current = current._parent;
            }

            return false;
        }

        private static bool TryNormalizeDetails(
            string name,
            string? description,
            out string normalizedName,
            out string? normalizedDescription,
            out string? error)
        {
            normalizedName = name?.Trim() ?? string.Empty;
            normalizedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

            if (normalizedName.Length == 0)
            {
                error = "Название категории обязательно.";
                return false;
            }

            if (normalizedName.Length > MAX_NAME_LENGTH)
            {
                error = $"Название категории не должно превышать {MAX_NAME_LENGTH} символов.";
                return false;
            }

            if (normalizedDescription?.Length > MAX_DESCRIPTION_LENGTH)
            {
                error = $"Описание категории не должно превышать {MAX_DESCRIPTION_LENGTH} символов.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
