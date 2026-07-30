using EFCoreLibrary.Abstractions.Entity;

namespace CostVision.Domain.Models.Receipts
{
    public class Store : IEntity<Guid>
    {
        public const int MAX_NAME_LENGTH = 256;
        public const int MAX_ADDRESS_LENGTH = 512;
        public const int MAX_ADAPTIVE_NAME_LENGTH = 500;

        internal Store()
        {
        }

        public Guid Id { get; set; }

        public string Name { get; internal set; } = string.Empty;

        public string NormalizedName { get; internal set; } = string.Empty;

        public string Address { get; internal set; } = string.Empty;

        public string NormalizedAddress { get; internal set; } = string.Empty;

        public string? AdaptiveName { get; internal set; }

        public ICollection<Receipt> Receipts { get; set; } = new List<Receipt>();

        /// <summary>
        /// Создаёт магазин с допустимыми начальными данными.
        /// </summary>
        public static bool TryCreate(
            string? name,
            string? normalizedName,
            string? address,
            string? normalizedAddress,
            out Store? store,
            out string? error)
        {
            store = null;

            if (!TryNormalizeDetails(
                    name,
                    normalizedName,
                    address,
                    normalizedAddress,
                    out string normalizedDisplayName,
                    out string normalizedNameKey,
                    out string normalizedDisplayAddress,
                    out string normalizedAddressKey,
                    out error))
                return false;

            store = new Store
            {
                Name = normalizedDisplayName,
                NormalizedName = normalizedNameKey,
                Address = normalizedDisplayAddress,
                NormalizedAddress = normalizedAddressKey
            };
            return true;
        }

        /// <summary>
        /// Обновляет отображаемые данные магазина.
        /// </summary>
        public bool TryUpdateDetails(
            string? name,
            string? normalizedName,
            string? address,
            string? normalizedAddress,
            out string? error)
        {
            if (!TryNormalizeDetails(
                    name,
                    normalizedName,
                    address,
                    normalizedAddress,
                    out string normalizedDisplayName,
                    out string normalizedNameKey,
                    out string normalizedDisplayAddress,
                    out string normalizedAddressKey,
                    out error))
                return false;

            Name = normalizedDisplayName;
            NormalizedName = normalizedNameKey;
            Address = normalizedDisplayAddress;
            NormalizedAddress = normalizedAddressKey;
            return true;
        }

        /// <summary>
        /// Обновляет адаптивное название магазина.
        /// </summary>
        public bool TryUpdateAdaptiveName(string? adaptiveName, out string? error)
        {
            if (!TryNormalizeAdaptiveName(adaptiveName, out string? normalizedAdaptiveName, out error))
                return false;

            AdaptiveName = normalizedAdaptiveName;
            return true;
        }

        /// <summary>
        /// Нормализует и проверяет адаптивное название магазина.
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
            string? address,
            string? normalizedAddress,
            out string normalizedDisplayName,
            out string normalizedNameKey,
            out string normalizedDisplayAddress,
            out string normalizedAddressKey,
            out string? error)
        {
            normalizedDisplayName = string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim();
            normalizedNameKey = normalizedName?.Trim() ?? string.Empty;
            normalizedDisplayAddress = string.IsNullOrWhiteSpace(address) ? string.Empty : address.Trim();
            normalizedAddressKey = normalizedAddress?.Trim() ?? string.Empty;

            if (normalizedDisplayName.Length == 0 && normalizedDisplayAddress.Length == 0)
            {
                error = "Название или адрес магазина должны быть заполнены.";
                return false;
            }

            if (normalizedDisplayName.Length > MAX_NAME_LENGTH || normalizedNameKey.Length > MAX_NAME_LENGTH)
            {
                error = $"Название магазина не должно превышать {MAX_NAME_LENGTH} символов.";
                return false;
            }

            if (normalizedDisplayAddress.Length > MAX_ADDRESS_LENGTH || normalizedAddressKey.Length > MAX_ADDRESS_LENGTH)
            {
                error = $"Адрес магазина не должен превышать {MAX_ADDRESS_LENGTH} символов.";
                return false;
            }

            if (normalizedDisplayName.Length > 0 && normalizedNameKey.Length == 0 ||
                normalizedDisplayAddress.Length > 0 && normalizedAddressKey.Length == 0)
            {
                error = "Нормализованные данные магазина не заполнены.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
