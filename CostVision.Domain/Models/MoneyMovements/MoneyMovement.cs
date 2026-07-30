using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.Enums.MoneyMovements;
using CostVision.Domain.Models.Receipts;
using EFCoreLibrary.Abstractions.Entity;
using System.Collections.ObjectModel;

namespace CostVision.Domain.Models.MoneyMovements
{
    /// <summary>
    /// Операция движения денег по счёту.
    /// </summary>
    public class MoneyMovement : IEntity<Guid>
    {
        public const int COMMENT_MAX_LENGTH = 1024;

        private readonly List<MoneyMovementReceipt> _receiptLinks = new();
        private readonly ReadOnlyCollection<MoneyMovementReceipt> _receiptLinksView;

        private MoneyMovement()
        {
            _receiptLinksView = _receiptLinks.AsReadOnly();
        }

        public Guid Id { get; set; }

        public Guid AccountId { get; private set; }

        public Account? Account { get; private set; }

        public decimal Amount { get; private set; }

        public MoneyMovementType Type { get; private set; }

        public DateTime OccurredAt { get; private set; }

        public string? Comment { get; private set; }

        public string? ImportComment { get; private set; }

        public Guid CreatedByUserId { get; private set; }

        public User? CreatedByUser { get; private set; }

        public Guid PerformedByUserId { get; private set; }

        public User? PerformedByUser { get; private set; }

        public DateTime CreatedAtUtc { get; private set; }

        public DateTime? UpdatedAtUtc { get; private set; }

        public MoneyMovementSource Source { get; private set; }

        public IReadOnlyCollection<MoneyMovementReceipt> ReceiptLinks => _receiptLinksView;

        /// <summary>
        /// Создаёт ручную операцию с допустимыми начальными данными.
        /// </summary>
        public static bool TryCreateManual(
            Guid accountId,
            decimal amount,
            MoneyMovementType? type,
            DateTime occurredAt,
            string? comment,
            Guid createdByUserId,
            Guid performedByUserId,
            DateTime createdAtUtc,
            out MoneyMovement? movement,
            out string? error)
        {
            movement = null;
            MoneyMovementType movementType = type ?? (amount < 0 ? MoneyMovementType.Expense : MoneyMovementType.Income);

            if (!TryNormalizeCommon(
                    accountId,
                    amount,
                    movementType,
                    occurredAt,
                    comment,
                    createdByUserId,
                    performedByUserId,
                    out decimal normalizedAmount,
                    out string? normalizedComment,
                    out error))
                return false;

            if (createdAtUtc == default)
            {
                error = "Дата создания операции не заполнена.";
                return false;
            }

            movement = new MoneyMovement
            {
                AccountId = accountId,
                Amount = normalizedAmount,
                Type = movementType,
                OccurredAt = occurredAt,
                Comment = normalizedComment,
                CreatedByUserId = createdByUserId,
                PerformedByUserId = performedByUserId,
                CreatedAtUtc = createdAtUtc,
                Source = MoneyMovementSource.Manual
            };
            return true;
        }

        /// <summary>
        /// Создаёт ручную операцию и согласованно задаёт загруженные навигации.
        /// </summary>
        public static bool TryCreateManual(
            Account account,
            decimal amount,
            MoneyMovementType? type,
            DateTime occurredAt,
            string? comment,
            User createdByUser,
            User performedByUser,
            DateTime createdAtUtc,
            out MoneyMovement? movement,
            out string? error)
        {
            movement = null;

            if (account == null || createdByUser == null || performedByUser == null)
            {
                error = "Счёт или пользователь операции не указан.";
                return false;
            }

            if (!TryCreateManual(
                    account.Id,
                    amount,
                    type,
                    occurredAt,
                    comment,
                    createdByUser.Id,
                    performedByUser.Id,
                    createdAtUtc,
                    out movement,
                    out error))
                return false;

            movement!.Account = account;
            movement.CreatedByUser = createdByUser;
            movement.PerformedByUser = performedByUser;
            return true;
        }

        /// <summary>
        /// Создаёт операцию банковского импорта с допустимыми начальными данными.
        /// </summary>
        public static bool TryCreateBankStatementImport(
            Guid accountId,
            decimal amount,
            MoneyMovementType type,
            DateTime occurredAt,
            string? comment,
            string? importComment,
            Guid userId,
            DateTime createdAtUtc,
            out MoneyMovement? movement,
            out string? error)
        {
            movement = null;

            if (amount <= 0)
            {
                error = "Сумма импортируемой операции должна быть больше нуля.";
                return false;
            }

            if (!TryNormalizeCommon(
                    accountId,
                    amount,
                    type,
                    occurredAt,
                    comment,
                    userId,
                    userId,
                    out decimal normalizedAmount,
                    out string? normalizedComment,
                    out error) ||
                !TryNormalizeImportComment(importComment, out string normalizedImportComment, out error))
                return false;

            if (createdAtUtc == default)
            {
                error = "Дата создания операции не заполнена.";
                return false;
            }

            movement = new MoneyMovement
            {
                AccountId = accountId,
                Amount = normalizedAmount,
                Type = type,
                OccurredAt = occurredAt,
                Comment = normalizedComment,
                ImportComment = normalizedImportComment,
                CreatedByUserId = userId,
                PerformedByUserId = userId,
                CreatedAtUtc = createdAtUtc,
                Source = MoneyMovementSource.BankStatementImport
            };
            return true;
        }

        /// <summary>
        /// Создаёт импортированную операцию и согласованно задаёт загруженные навигации.
        /// </summary>
        public static bool TryCreateBankStatementImport(
            Account account,
            decimal amount,
            MoneyMovementType type,
            DateTime occurredAt,
            string? comment,
            string? importComment,
            User user,
            DateTime createdAtUtc,
            out MoneyMovement? movement,
            out string? error)
        {
            movement = null;

            if (account == null || user == null)
            {
                error = "Счёт или пользователь операции не указан.";
                return false;
            }

            if (!TryCreateBankStatementImport(
                    account.Id,
                    amount,
                    type,
                    occurredAt,
                    comment,
                    importComment,
                    user.Id,
                    createdAtUtc,
                    out movement,
                    out error))
                return false;

            movement!.Account = account;
            movement.CreatedByUser = user;
            movement.PerformedByUser = user;
            return true;
        }

        /// <summary>
        /// Заменяет данные операции значениями из банковской выписки.
        /// </summary>
        public bool TryReplaceFromBankStatement(
            decimal amount,
            MoneyMovementType type,
            DateTime occurredAt,
            string? comment,
            string? importComment,
            DateTime updatedAtUtc,
            out string? error)
        {
            if (amount <= 0)
            {
                error = "Сумма импортируемой операции должна быть больше нуля.";
                return false;
            }

            if (!TryNormalizeCommon(
                    AccountId,
                    amount,
                    type,
                    occurredAt,
                    comment,
                    CreatedByUserId,
                    PerformedByUserId,
                    out decimal normalizedAmount,
                    out string? normalizedComment,
                    out error) ||
                !TryNormalizeImportComment(importComment, out string normalizedImportComment, out error))
                return false;

            if (updatedAtUtc == default)
            {
                error = "Дата изменения операции не заполнена.";
                return false;
            }

            Amount = normalizedAmount;
            Type = type;
            OccurredAt = occurredAt;
            Comment = normalizedComment;
            ImportComment = normalizedImportComment;
            Source = MoneyMovementSource.BankStatementImport;
            UpdatedAtUtc = updatedAtUtc;
            return true;
        }

        /// <summary>
        /// Обновляет пользовательский комментарий операции.
        /// </summary>
        public bool TryUpdateComment(string? comment, out string? error)
        {
            if (!TryNormalizeComment(comment, out string? normalizedComment, out error))
                return false;

            Comment = normalizedComment;
            return true;
        }

        /// <summary>
        /// Переносит операцию на другой счёт.
        /// </summary>
        public bool TryMoveToAccount(Guid accountId, out string? error)
        {
            if (accountId == Guid.Empty)
            {
                error = "Некорректный идентификатор счёта.";
                return false;
            }

            AccountId = accountId;
            error = null;
            return true;
        }

        /// <summary>
        /// Переносит операцию на указанный счёт и согласованно задаёт навигацию.
        /// </summary>
        public bool TryMoveToAccount(Account account, out string? error)
        {
            if (account == null)
            {
                error = "Счёт не указан.";
                return false;
            }

            if (!TryMoveToAccount(account.Id, out error))
                return false;

            Account = account;
            return true;
        }

        internal bool TryAttachReceiptLink(MoneyMovementReceipt link, out string? error)
        {
            if (link == null ||
                link.MoneyMovementId != Id ||
                !ReferenceEquals(link.MoneyMovement, this) ||
                link.ReceiptId == Guid.Empty ||
                link.CreatedByUserId == Guid.Empty ||
                link.CreatedAtUtc == default)
            {
                error = "Некорректная связь операции с чеком.";
                return false;
            }

            if (_receiptLinks.Any(item => item.ReceiptId == link.ReceiptId))
            {
                error = "Чек уже привязан к операции.";
                return false;
            }

            _receiptLinks.Add(link);
            error = null;
            return true;
        }

        private static bool TryNormalizeCommon(
            Guid accountId,
            decimal amount,
            MoneyMovementType type,
            DateTime occurredAt,
            string? comment,
            Guid createdByUserId,
            Guid performedByUserId,
            out decimal normalizedAmount,
            out string? normalizedComment,
            out string? error)
        {
            normalizedAmount = 0;
            normalizedComment = null;

            if (accountId == Guid.Empty)
            {
                error = "Некорректный идентификатор счёта.";
                return false;
            }

            if (amount == 0 || amount == decimal.MinValue)
            {
                error = "Сумма операции должна быть больше нуля.";
                return false;
            }

            if (!Enum.IsDefined(type))
            {
                error = "Некорректный тип операции.";
                return false;
            }

            if (occurredAt == default)
            {
                error = "Дата операции не заполнена.";
                return false;
            }

            if (createdByUserId == Guid.Empty || performedByUserId == Guid.Empty)
            {
                error = "Некорректный идентификатор пользователя операции.";
                return false;
            }

            if (!TryNormalizeComment(comment, out normalizedComment, out error))
                return false;

            normalizedAmount = Math.Abs(amount);
            return true;
        }

        private static bool TryNormalizeComment(string? comment, out string? normalizedComment, out string? error)
        {
            normalizedComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();

            if (normalizedComment?.Length > COMMENT_MAX_LENGTH)
            {
                error = $"Комментарий не должен превышать {COMMENT_MAX_LENGTH} символа.";
                return false;
            }

            error = null;
            return true;
        }

        private static bool TryNormalizeImportComment(string? importComment, out string normalizedImportComment, out string? error)
        {
            normalizedImportComment = importComment?.Trim() ?? string.Empty;

            if (normalizedImportComment.Length == 0)
            {
                error = "У импортируемой операции отсутствует исходный комментарий.";
                return false;
            }

            if (normalizedImportComment.Length > COMMENT_MAX_LENGTH)
            {
                error = $"Исходный комментарий не должен превышать {COMMENT_MAX_LENGTH} символа.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
