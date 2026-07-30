using CostVision.Domain.Models.MoneyMovements;
using CostVision.Domain.Models.Receipts;
using EFCoreLibrary.Abstractions.Entity;
using System.Collections.ObjectModel;

namespace CostVision.Domain.Models.Authorization
{
    public class User : IEntity<Guid>
    {
        public const int MIN_LOGIN_LENGTH = 3;
        public const int MAX_LOGIN_LENGTH = 128;
        public const int MIN_NAME_LENGTH = 2;
        public const int MAX_NAME_LENGTH = 256;
        public const int PASSWORD_HASH_MAX_LENGTH = 512;

        private readonly List<Role> _roles = new();
        private readonly ReadOnlyCollection<Role> _rolesView;
        private readonly List<UserRole> _userRoles = new();
        private readonly ReadOnlyCollection<UserRole> _userRolesView;
        private readonly List<AccountMember> _accountMemberships = new();
        private readonly ReadOnlyCollection<AccountMember> _accountMembershipsView;
        private readonly List<Receipt> _createdReceipts = new();
        private readonly ReadOnlyCollection<Receipt> _createdReceiptsView;
        private readonly List<ExpenseCategory> _categories = new();
        private readonly ReadOnlyCollection<ExpenseCategory> _categoriesView;
        private readonly List<Account> _accounts = new();
        private readonly ReadOnlyCollection<Account> _accountsView;
        private readonly List<MoneyMovement> _createdMoneyMovements = new();
        private readonly ReadOnlyCollection<MoneyMovement> _createdMoneyMovementsView;
        private readonly List<MoneyMovement> _performedMoneyMovements = new();
        private readonly ReadOnlyCollection<MoneyMovement> _performedMoneyMovementsView;
        private readonly List<MoneyMovementReceipt> _createdMoneyMovementReceiptLinks = new();
        private readonly ReadOnlyCollection<MoneyMovementReceipt> _createdMoneyMovementReceiptLinksView;

        private User()
        {
            _rolesView = _roles.AsReadOnly();
            _userRolesView = _userRoles.AsReadOnly();
            _accountMembershipsView = _accountMemberships.AsReadOnly();
            _createdReceiptsView = _createdReceipts.AsReadOnly();
            _categoriesView = _categories.AsReadOnly();
            _accountsView = _accounts.AsReadOnly();
            _createdMoneyMovementsView = _createdMoneyMovements.AsReadOnly();
            _performedMoneyMovementsView = _performedMoneyMovements.AsReadOnly();
            _createdMoneyMovementReceiptLinksView = _createdMoneyMovementReceiptLinks.AsReadOnly();
        }

        public Guid Id { get; set; }

        public string Login { get; private set; } = string.Empty;

        public string Name { get; private set; } = string.Empty;

        public string PasswordHash { get; private set; } = string.Empty;

        public bool IsActive { get; private set; }

        public DateTime CreatedAtUtc { get; private set; }

        public DateTime? LastLoginAtUtc { get; private set; }

        public IReadOnlyCollection<Role> Roles => _rolesView;

        public IReadOnlyCollection<UserRole> UserRoles => _userRolesView;

        public IReadOnlyCollection<AccountMember> AccountMemberships => _accountMembershipsView;

        public IReadOnlyCollection<Receipt> CreatedReceipts => _createdReceiptsView;

        public IReadOnlyCollection<ExpenseCategory> Categories => _categoriesView;

        public IReadOnlyCollection<Account> Accounts => _accountsView;

        public IReadOnlyCollection<MoneyMovement> CreatedMoneyMovements => _createdMoneyMovementsView;

        public IReadOnlyCollection<MoneyMovement> PerformedMoneyMovements => _performedMoneyMovementsView;

        public IReadOnlyCollection<MoneyMovementReceipt> CreatedMoneyMovementReceiptLinks => _createdMoneyMovementReceiptLinksView;

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
        /// Создаёт допустимого активного пользователя с назначенными ролями.
        /// </summary>
        public static bool TryCreate(
            string login,
            string name,
            string passwordHash,
            IEnumerable<Role> roles,
            DateTime createdAtUtc,
            out User? user,
            out string? error)
        {
            user = null;
            List<Role> normalizedRoles = roles?
                .Where(role => role != null && role.Id != Guid.Empty)
                .DistinctBy(role => role.Id)
                .ToList() ?? new List<Role>();

            if (normalizedRoles.Count == 0)
            {
                error = "Роль обязательна.";
                return false;
            }

            if (!TryCreate(
                    login,
                    name,
                    passwordHash,
                    normalizedRoles.Select(role => role.Id),
                    createdAtUtc,
                    out user,
                    out error))
                return false;

            user!._userRoles.Clear();
            foreach (Role role in normalizedRoles)
            {
                user._roles.Add(role);
                user._userRoles.Add(UserRole.Create(user, role));
            }

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
            _roles.RemoveAll(role => !desiredRoleIds.Contains(role.Id));

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
        public bool TryMarkLogin(DateTime loginAtUtc, out string? error)
        {
            return TryMarkActivity(loginAtUtc, out error);
        }

        /// <summary>
        /// Обновляет время последней активности пользователя.
        /// </summary>
        public bool TryMarkActivity(DateTime activityAtUtc, out string? error)
        {
            if (activityAtUtc == default)
            {
                error = "Дата активности пользователя не заполнена.";
                return false;
            }

            LastLoginAtUtc = activityAtUtc;
            error = null;
            return true;
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
