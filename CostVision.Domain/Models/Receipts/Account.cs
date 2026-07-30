using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.MoneyMovements;
using EFCoreLibrary.Abstractions.Entity;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using CostVision.Domain.Models.Enums.Authorization;

namespace CostVision.Domain.Models.Receipts
{
    /// <summary>
    /// Счёт, кошелёк или общий бюджет для группировки чеков и расходов.
    /// </summary>
    public class Account : IEntity<Guid>
    {
        public const string DEFAULT_COLOR_HEX = "#0D6EFD";
        public const int MIN_NAME_LENGTH = 3;
        public const int MAX_NAME_LENGTH = 128;
        public const int MAX_DESCRIPTION_LENGTH = 512;

        private static readonly Regex COLOR_HEX_REGEX = new("^#[0-9A-F]{6}$", RegexOptions.Compiled);
        private readonly List<AccountMember> _members = new();
        private readonly ReadOnlyCollection<AccountMember> _membersView;
        private readonly List<ReceiptAccount> _receiptLinks = new();
        private readonly ReadOnlyCollection<ReceiptAccount> _receiptLinksView;
        private readonly List<MoneyMovement> _moneyMovements = new();
        private readonly ReadOnlyCollection<MoneyMovement> _moneyMovementsView;

        private Account()
        {
            _membersView = _members.AsReadOnly();
            _receiptLinksView = _receiptLinks.AsReadOnly();
            _moneyMovementsView = _moneyMovements.AsReadOnly();
        }

        public Guid Id { get; set; }

        public string Name { get; private set; } = string.Empty;

        public string? Description { get; private set; }

        /// <summary>
        /// Цвет счёта в формате HEX.
        /// </summary>
        public string ColorHex { get; private set; } = DEFAULT_COLOR_HEX;

        public bool IsArchived { get; private set; }

        public DateTime CreatedAtUtc { get; private set; }

        public Guid CreatedByUserId { get; private set; }

        public User? CreatedByUser { get; private set; }

        public IReadOnlyCollection<AccountMember> Members => _membersView;

        public IReadOnlyCollection<ReceiptAccount> ReceiptLinks => _receiptLinksView;

        public IReadOnlyCollection<MoneyMovement> MoneyMovements => _moneyMovementsView;

        /// <summary>
        /// Создаёт счёт с допустимыми начальными данными.
        /// </summary>
        public static bool TryCreate(
            string name,
            string? description,
            string? colorHex,
            Guid createdByUserId,
            DateTime createdAtUtc,
            out Account? account,
            out string? error)
        {
            account = null;

            if (createdByUserId == Guid.Empty)
            {
                error = "Некорректный идентификатор владельца счёта.";
                return false;
            }

            if (createdAtUtc == default)
            {
                error = "Дата создания счёта не заполнена.";
                return false;
            }

            if (!TryNormalizeDetails(name, description, colorHex, out string normalizedName, out string? normalizedDescription, out string normalizedColorHex, out error))
                return false;

            account = new Account
            {
                Name = normalizedName,
                Description = normalizedDescription,
                ColorHex = normalizedColorHex,
                CreatedByUserId = createdByUserId,
                CreatedAtUtc = createdAtUtc
            };
            account._members.Add(AccountMember.CreateOwner(account, createdByUserId));

            return true;
        }

        /// <summary>
        /// Создаёт счёт для указанного владельца и согласованно задаёт навигацию владельца.
        /// </summary>
        public static bool TryCreate(
            string name,
            string? description,
            string? colorHex,
            User createdByUser,
            DateTime createdAtUtc,
            out Account? account,
            out string? error)
        {
            account = null;

            if (createdByUser == null)
            {
                error = "Владелец счёта не указан.";
                return false;
            }

            if (!TryCreate(
                    name,
                    description,
                    colorHex,
                    createdByUser.Id,
                    createdAtUtc,
                    out account,
                    out error))
                return false;

            account!.CreatedByUser = createdByUser;
            return true;
        }

        /// <summary>
        /// Изменяет основные данные счёта, сохраняя их допустимое состояние.
        /// </summary>
        public bool TryUpdateDetails(string name, string? description, string? colorHex, out string? error)
        {
            if (!TryNormalizeDetails(name, description, colorHex, out string normalizedName, out string? normalizedDescription, out string normalizedColorHex, out error))
                return false;

            Name = normalizedName;
            Description = normalizedDescription;
            ColorHex = normalizedColorHex;
            return true;
        }

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
        /// Проверяет, можно ли назначить пользователю указанную роль в счёте.
        /// </summary>
        public bool CanAssignMember(Guid userId, AccountAccessRole role, out string? error)
        {
            if (userId == Guid.Empty)
            {
                error = "Некорректный идентификатор участника счёта.";
                return false;
            }

            if (userId == CreatedByUserId)
            {
                error = "Нельзя изменять участие владельца счёта.";
                return false;
            }

            if (role != AccountAccessRole.Viewer && role != AccountAccessRole.Editor)
            {
                error = "Можно назначить только роли Viewer или Editor.";
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>
        /// Добавляет нового участника счёта.
        /// </summary>
        public bool TryAddMember(Guid userId, AccountAccessRole role, out AccountMember? member, out string? error)
        {
            member = null;

            if (!CanAssignMember(userId, role, out error))
                return false;

            if (HasMember(userId))
            {
                error = "Пользователь уже является участником счёта.";
                return false;
            }

            member = AccountMember.Create(this, userId, role);
            _members.Add(member);
            return true;
        }

        /// <summary>
        /// Изменяет роль участника счёта.
        /// </summary>
        public bool TryChangeMemberRole(Guid userId, AccountAccessRole role, out string? error)
        {
            if (!CanAssignMember(userId, role, out error))
                return false;

            AccountMember? member = _members.FirstOrDefault(item => item.UserId == userId);
            if (member == null)
            {
                error = "Пользователь не является участником счёта.";
                return false;
            }

            member.ChangeRole(role);
            return true;
        }

        /// <summary>
        /// Удаляет участника из счёта.
        /// </summary>
        public bool TryRemoveMember(Guid userId, out AccountMember? member, out string? error)
        {
            member = null;

            if (userId == Guid.Empty)
            {
                error = "Некорректный идентификатор участника счёта.";
                return false;
            }

            if (userId == CreatedByUserId)
            {
                error = "Нельзя удалить владельца счёта.";
                return false;
            }

            member = _members.FirstOrDefault(item => item.UserId == userId);
            if (member == null)
            {
                error = "Пользователь не является участником счёта.";
                return false;
            }

            _members.Remove(member);
            error = null;
            return true;
        }

        /// <summary>
        /// Проверяет, состоит ли пользователь в участниках счёта.
        /// </summary>
        public bool HasMember(Guid userId)
        {
            return _members.Any(member => member.UserId == userId);
        }

        private static bool TryNormalizeDetails(
            string name,
            string? description,
            string? colorHex,
            out string normalizedName,
            out string? normalizedDescription,
            out string normalizedColorHex,
            out string? error)
        {
            normalizedName = name?.Trim() ?? string.Empty;
            normalizedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
            normalizedColorHex = string.IsNullOrWhiteSpace(colorHex) ? DEFAULT_COLOR_HEX : colorHex.Trim().ToUpperInvariant();

            if (normalizedName.Length < MIN_NAME_LENGTH)
            {
                error = $"Название счёта обязательно. Минимум {MIN_NAME_LENGTH} символа.";
                return false;
            }

            if (normalizedName.Length > MAX_NAME_LENGTH)
            {
                error = $"Название счёта не должно превышать {MAX_NAME_LENGTH} символов.";
                return false;
            }

            if (normalizedDescription?.Length > MAX_DESCRIPTION_LENGTH)
            {
                error = $"Описание счёта не должно превышать {MAX_DESCRIPTION_LENGTH} символов.";
                return false;
            }

            if (!COLOR_HEX_REGEX.IsMatch(normalizedColorHex))
            {
                error = "Некорректный цвет счёта.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
