using CostVision.Domain.Models.Enums.Authorization;
using EFCoreLibrary.Abstractions.Entity;
using System.Collections.ObjectModel;

namespace CostVision.Domain.Models.Authorization
{
    public class Role : IEntity<Guid>
    {
        public const int MAX_NAME_LENGTH = 128;

        private readonly List<User> _users = new();
        private readonly ReadOnlyCollection<User> _usersView;
        private readonly List<UserRole> _userRoles = new();
        private readonly ReadOnlyCollection<UserRole> _userRolesView;

        private Role()
        {
            _usersView = _users.AsReadOnly();
            _userRolesView = _userRoles.AsReadOnly();
        }

        public Guid Id { get; set; }

        public string Name { get; private set; } = string.Empty;

        public RoleType RoleType { get; private set; } = RoleType.User;

        public IReadOnlyCollection<User> Users => _usersView;

        public IReadOnlyCollection<UserRole> UserRoles => _userRolesView;

        /// <summary>
        /// Создаёт роль с допустимыми начальными данными.
        /// </summary>
        public static bool TryCreate(string name, RoleType roleType, out Role? role, out string? error)
        {
            role = null;

            if (!TryNormalizeDetails(name, roleType, out string normalizedName, out error))
                return false;

            role = new Role
            {
                Name = normalizedName,
                RoleType = roleType
            };
            return true;
        }

        /// <summary>
        /// Изменяет название и тип роли.
        /// </summary>
        public bool TryUpdateDetails(string name, RoleType roleType, out string? error)
        {
            if (!TryNormalizeDetails(name, roleType, out string normalizedName, out error))
                return false;

            Name = normalizedName;
            RoleType = roleType;
            return true;
        }

        private static bool TryNormalizeDetails(
            string name,
            RoleType roleType,
            out string normalizedName,
            out string? error)
        {
            normalizedName = name?.Trim() ?? string.Empty;

            if (normalizedName.Length == 0)
            {
                error = "Название роли обязательно.";
                return false;
            }

            if (normalizedName.Length > MAX_NAME_LENGTH)
            {
                error = $"Название роли не должно превышать {MAX_NAME_LENGTH} символов.";
                return false;
            }

            if (!Enum.IsDefined(roleType))
            {
                error = "Некорректный тип роли.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
