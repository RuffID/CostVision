using EFCoreLibrary.Abstractions.Entity;

namespace CostVision.Domain.Models.Receipts
{
    public class Product : IEntity<Guid>
    {
        public const int MAX_NAME_LENGTH = 500;
        public const int MAX_ADAPTIVE_NAME_LENGTH = 500;

        internal Product()
        {
        }

        public Guid Id { get; set; }

        public string Name { get; internal set; } = string.Empty;

        public string NormalizedName { get; internal set; } = string.Empty;

        public string? AdaptiveName { get; internal set; }

        public ICollection<ReceiptItem> ReceiptItems { get; set; } = new List<ReceiptItem>();

        /// <summary>
        /// Создаёт товар с допустимыми начальными данными.
        /// </summary>
        public static bool TryCreate(string? name, string? normalizedName, out Product? product, out string? error)
        {
            product = null;

            if (!TryNormalizeDetails(name, normalizedName, out string normalizedDisplayName, out string normalizedKey, out error))
                return false;

            product = new Product
            {
                Name = normalizedDisplayName,
                NormalizedName = normalizedKey
            };
            return true;
        }

        /// <summary>
        /// Обновляет отображаемые данные товара.
        /// </summary>
        public bool TryUpdateDetails(string? name, string? normalizedName, out string? error)
        {
            if (!TryNormalizeDetails(name, normalizedName, out string normalizedDisplayName, out string normalizedKey, out error))
                return false;

            Name = normalizedDisplayName;
            NormalizedName = normalizedKey;
            return true;
        }

        /// <summary>
        /// Обновляет адаптивное название товара.
        /// </summary>
        public bool TryUpdateAdaptiveName(string? adaptiveName, out string? error)
        {
            if (!TryNormalizeAdaptiveName(adaptiveName, out string? normalizedAdaptiveName, out error))
                return false;

            AdaptiveName = normalizedAdaptiveName;
            return true;
        }

        /// <summary>
        /// Нормализует и проверяет адаптивное название товара.
        /// </summary>
        public static bool TryNormalizeAdaptiveName(string? adaptiveName, out string? normalizedAdaptiveName, out string? error)
        {
            normalizedAdaptiveName = string.IsNullOrWhiteSpace(adaptiveName) ? null : adaptiveName.Trim();

            if (normalizedAdaptiveName?.Length > MAX_ADAPTIVE_NAME_LENGTH)
            {
                error = $"Адаптивное название не должно быть длиннее {MAX_ADAPTIVE_NAME_LENGTH} символов.";
                return false;
            }

            error = null;
            return true;
        }

        private static bool TryNormalizeDetails(
            string? name,
            string? normalizedName,
            out string normalizedDisplayName,
            out string normalizedKey,
            out string? error)
        {
            normalizedDisplayName = name?.Trim() ?? string.Empty;
            normalizedKey = normalizedName?.Trim() ?? string.Empty;

            if (normalizedDisplayName.Length == 0 || normalizedKey.Length == 0)
            {
                error = "Название товара обязательно.";
                return false;
            }

            if (normalizedDisplayName.Length > MAX_NAME_LENGTH || normalizedKey.Length > MAX_NAME_LENGTH)
            {
                error = $"Название товара не должно превышать {MAX_NAME_LENGTH} символов.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
