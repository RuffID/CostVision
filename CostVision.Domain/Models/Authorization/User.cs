using CostVision.Domain.Models.MoneyMovements;
using CostVision.Domain.Models.Receipts;
using EFCoreLibrary.Abstractions.Entity;

namespace CostVision.Domain.Models.Authorization
{
    public class User : IEntity<Guid>
    {
        public const int MIN_LOGIN_LENGTH = 3;
        public const int MAX_LOGIN_LENGTH = 128;
        public const int MIN_NAME_LENGTH = 2;
        public const int MAX_NAME_LENGTH = 256;
        public const int PASSWORD_HASH_MAX_LENGTH = 512;

        private readonly List<UserRole> _userRoles = new();

        internal User()
        {
        }

        public Guid Id { get; set; }

        public string Login { get; internal set; } = string.Empty;

        public string Name { get; internal set; } = string.Empty;

        public string PasswordHash { get; internal set; } = string.Empty;

        public bool IsActive { get; internal set; }

        public DateTime CreatedAtUtc { get; internal set; }

        public DateTime? LastLoginAtUtc { get; internal set; }

        public ICollection<Role> Roles { get; set; } = new List<Role>();

        public IReadOnlyCollection<UserRole> UserRoles => _userRoles;

        public List<AccountMember> AccountMemberships { get; set; } = new List<AccountMember>();

        public List<Receipt> CreatedReceipts { get; set; } = new List<Receipt>();

        public List<ExpenseCategory> Categories { get; set; } = new List<ExpenseCategory>();

        public List<Account> Accounts { get; set; } = new List<Account>();

        public List<MoneyMovement> CreatedMoneyMovements { get; set; } = new List<MoneyMovement>();

        public List<MoneyMovement> PerformedMoneyMovements { get; set; } = new List<MoneyMovement>();

        public List<MoneyMovementReceipt> CreatedMoneyMovementReceiptLinks { get; set; } = new List<MoneyMovementReceipt>();

        /// <summary>
        /// Создаёт допустимого активного пользователя с назначенными ролями.
        /// </summary>
        public static bool TryCreate(
            string login,
            string name,
            string passwordHash,
            IEnumerable<Guid> roleIds,
            DateTime createdAtUtc,
            out User? user,
            out string? error)
        {
            user = null;

            if (!TryNormalizeProfile(login, name, out string normalizedLogin, out string normalizedName, out error) ||
                !TryValidatePasswordHash(passwordHash, out error) ||
                !TryNormalizeRoleIds(roleIds, out List<Guid> normalizedRoleIds, out error))
                return false;

            if (createdAtUtc == default)
            {
                error = "Дата создания пользователя не заполнена.";
                return false;
            }

            user = new User
            {
                Login = normalizedLogin,
                Name = normalizedName,
                PasswordHash = passwordHash,
                IsActive = true,
                CreatedAtUtc = createdAtUtc
            };

            foreach (Guid roleId in normalizedRoleIds)
                user._userRoles.Add(UserRole.Create(user, roleId));

            error = null;
            return true;
        }

        /// <summary>
        /// Атомарно обновляет профиль, хеш пароля и назначенные роли пользователя.
        /// </summary>
        public bool TryUpdate(
            string login,
            string name,
            string? passwordHash,
            IEnumerable<Guid> roleIds,
            out string? error)
        {
            if (!TryNormalizeProfile(login, name, out string normalizedLogin, out string normalizedName, out error) ||
                passwordHash != null && !TryValidatePasswordHash(passwordHash, out error) ||
                !TryNormalizeRoleIds(roleIds, out List<Guid> normalizedRoleIds, out error))
                return false;

            Login = normalizedLogin;
            Name = normalizedName;

            if (passwordHash != null)
                PasswordHash = passwordHash;

            HashSet<Guid> desiredRoleIds = normalizedRoleIds.ToHashSet();
            _userRoles.RemoveAll(userRole => !desiredRoleIds.Contains(userRole.RoleId));

            HashSet<Guid> currentRoleIds = _userRoles.Select(userRole => userRole.RoleId).ToHashSet();
            foreach (Guid roleId in normalizedRoleIds.Where(roleId => !currentRoleIds.Contains(roleId)))
                _userRoles.Add(UserRole.Create(this, roleId));

            error = null;
            return true;
        }

        /// <summary>
        /// Активирует пользователя.
        /// </summary>
        public void Activate()
        {
            IsActive = true;
        }

        /// <summary>
        /// Деактивирует пользователя.
        /// </summary>
        public void Deactivate()
        {
            IsActive = false;
        }

        /// <summary>
        /// Обновляет время последнего успешного входа.
        /// </summary>
        public void MarkLogin(DateTime loginAtUtc)
        {
            MarkActivity(loginAtUtc);
        }

        /// <summary>
        /// Обновляет время последней активности пользователя.
        /// </summary>
        public void MarkActivity(DateTime activityAtUtc)
        {
            LastLoginAtUtc = activityAtUtc;
        }

        private static bool TryNormalizeProfile(
            string login,
            string name,
            out string normalizedLogin,
            out string normalizedName,
            out string? error)
        {
            normalizedLogin = login?.Trim() ?? string.Empty;
            normalizedName = name?.Trim() ?? string.Empty;

            if (normalizedLogin.Length is < MIN_LOGIN_LENGTH or > MAX_LOGIN_LENGTH)
            {
                error = $"Логин должен быть от {MIN_LOGIN_LENGTH} до {MAX_LOGIN_LENGTH} символов.";
                return false;
            }

            if (normalizedName.Length is < MIN_NAME_LENGTH or > MAX_NAME_LENGTH)
            {
                error = $"Имя должно быть от {MIN_NAME_LENGTH} до {MAX_NAME_LENGTH} символов.";
                return false;
            }

            error = null;
            return true;
        }

        private static bool TryValidatePasswordHash(string passwordHash, out string? error)
        {
            if (string.IsNullOrWhiteSpace(passwordHash))
            {
                error = "Хеш пароля пользователя не заполнен.";
                return false;
            }

            if (passwordHash.Length > PASSWORD_HASH_MAX_LENGTH)
            {
                error = $"Хеш пароля не должен превышать {PASSWORD_HASH_MAX_LENGTH} символов.";
                return false;
            }

            error = null;
            return true;
        }

        private static bool TryNormalizeRoleIds(
            IEnumerable<Guid> roleIds,
            out List<Guid> normalizedRoleIds,
            out string? error)
        {
            normalizedRoleIds = roleIds?
                .Where(roleId => roleId != Guid.Empty)
                .Distinct()
                .ToList() ?? new List<Guid>();

            if (normalizedRoleIds.Count == 0)
            {
                error = "Роль обязательна.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
