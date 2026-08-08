using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.Enums.Receipts;
using CostVision.Domain.Models.MoneyMovements;
using EFCoreLibrary.Abstractions.Entity;
using System.Collections.ObjectModel;

namespace CostVision.Domain.Models.Receipts
{
    public class Receipt : IEntity<Guid>
    {
        public const int MAX_FISCAL_NUMBER_LENGTH = 32;
        public const int MAX_USER_LENGTH = 256;
        public const int MAX_USER_INN_LENGTH = 32;
        public const int MAX_KKT_ID_LENGTH = 64;
        public const int MAX_REGION_LENGTH = 32;
        public const int MAX_REFRESH_ERROR_LENGTH = 1000;

        private readonly List<ReceiptItem> _items = new();
        private readonly ReadOnlyCollection<ReceiptItem> _itemsView;
        private readonly List<ReceiptAccount> _accounts = new();
        private readonly ReadOnlyCollection<ReceiptAccount> _accountsView;
        private readonly List<MoneyMovementReceipt> _moneyMovementLinks = new();
        private readonly ReadOnlyCollection<MoneyMovementReceipt> _moneyMovementLinksView;

        private Receipt()
        {
            _itemsView = _items.AsReadOnly();
            _accountsView = _accounts.AsReadOnly();
            _moneyMovementLinksView = _moneyMovementLinks.AsReadOnly();
        }

        public Guid Id { get; set; }

        /// <summary>
        /// ФН (FN) - номер фискального накопителя.
        /// </summary>
        public string FiscalDriveNumber { get; private set; } = string.Empty;

        /// <summary>
        /// ФД (FD) - фискальный номер документа.
        /// </summary>
        public string FiscalDocumentNumber { get; private set; } = string.Empty;

        /// <summary>
        /// ФПД, ФП (FP) - фискальный признак документа.
        /// </summary>
        public string FiscalSign { get; private set; } = string.Empty;

        public Guid? StoreId { get; private set; }

        public Store? Store { get; private set; }

        /// <summary>
        /// Наименование юридического лица.
        /// </summary>
        public string? User { get; private set; }

        public string? UserInn { get; private set; }

        /// <summary>
        /// Время покупки.
        /// </summary>
        public DateTime DateTime { get; private set; }

        /// <summary>
        /// Номер чека за смену.
        /// </summary>
        public int? CheckNumber { get; private set; }

        public int? ShiftNumber { get; private set; }

        public ReceiptOperationType OperationType { get; private set; }

        public TaxationType? TaxationType { get; private set; }

        public decimal TotalSum { get; private set; }

        public decimal CashTotalSum { get; private set; }

        public decimal EcashTotalSum { get; private set; }

        /// <summary>
        /// Налоги.
        /// </summary>
        public decimal? Nds18 { get; private set; }

        public decimal? Nds10 { get; private set; }

        public decimal? Nds0 { get; private set; }

        public decimal? NdsNo { get; private set; }

        /// <summary>
        /// Информация о кассовой технике.
        /// </summary>
        public string? KktRegId { get; private set; }

        public string? NumberKkt { get; private set; }

        /// <summary>
        /// Регион или город покупки.
        /// </summary>
        public string? Region { get; private set; }

        /// <summary>
        /// Идентификатор пользователя, который импортировал чек.
        /// </summary>
        public Guid CreatedByUserId { get; private set; }

        public User? CreatedByUser { get; private set; }

        /// <summary>
        /// Позиции чека.
        /// </summary>
        public IReadOnlyCollection<ReceiptItem> Items => _itemsView;

        /// <summary>
        /// Связи чека со счетами.
        /// </summary>
        public IReadOnlyCollection<ReceiptAccount> Accounts => _accountsView;

        /// <summary>
        /// Связи чека с операциями движения денег.
        /// </summary>
        public IReadOnlyCollection<MoneyMovementReceipt> MoneyMovementLinks => _moneyMovementLinksView;

        /// <summary>
        /// Метка времени создания записи в системе.
        /// </summary>
        public DateTime CreatedAtUtc { get; private set; }

        public DateTime? UpdatedAtUtc { get; private set; }

        public ReceiptRefreshStatus RefreshStatus { get; private set; } = ReceiptRefreshStatus.Pending;

        public int RefreshAttemptCount { get; private set; }

        public DateTime? LastRefreshAttemptAtUtc { get; private set; }

        public DateTime? NextRefreshAttemptAtUtc { get; private set; }

        public string? LastRefreshError { get; private set; }

        /// <summary>
        /// Создаёт чек с допустимыми обязательными реквизитами.
        /// </summary>
        public static bool TryCreate(
            string? fiscalDriveNumber,
            string? fiscalDocumentNumber,
            string? fiscalSign,
            DateTime dateTime,
            ReceiptOperationType operationType,
            decimal totalSum,
            Guid createdByUserId,
            DateTime createdAtUtc,
            out Receipt? receipt,
            out string? error)
        {
            receipt = null;

            if (!TryNormalizeIdentity(
                    fiscalDriveNumber,
                    fiscalDocumentNumber,
                    fiscalSign,
                    out string normalizedFiscalDriveNumber,
                    out string normalizedFiscalDocumentNumber,
                    out string normalizedFiscalSign,
                    out error))
                return false;

            if (dateTime == default)
            {
                error = "Дата и время покупки не заполнены.";
                return false;
            }

            if (!Enum.IsDefined(operationType))
            {
                error = "Некорректный тип операции чека.";
                return false;
            }

            if (totalSum <= 0)
            {
                error = "Сумма чека должна быть больше нуля.";
                return false;
            }

            if (createdByUserId == Guid.Empty)
            {
                error = "Некорректный идентификатор пользователя, создавшего чек.";
                return false;
            }

            if (createdAtUtc == default)
            {
                error = "Дата создания чека не заполнена.";
                return false;
            }

            receipt = new Receipt
            {
                FiscalDriveNumber = normalizedFiscalDriveNumber,
                FiscalDocumentNumber = normalizedFiscalDocumentNumber,
                FiscalSign = normalizedFiscalSign,
                DateTime = dateTime,
                OperationType = operationType,
                TotalSum = totalSum,
                CreatedByUserId = createdByUserId,
                CreatedAtUtc = createdAtUtc,
                RefreshStatus = ReceiptRefreshStatus.Pending
            };
            return true;
        }

        /// <summary>
        /// Проверяет, наступило ли время следующей фоновой попытки обновления.
        /// </summary>
        public bool CanAttemptRefresh(DateTime utcNow, int maxAttempts)
        {
            return utcNow != default &&
                   maxAttempts > 0 &&
                   RefreshStatus == ReceiptRefreshStatus.Pending &&
                   RefreshAttemptCount < maxAttempts &&
                   (!NextRefreshAttemptAtUtc.HasValue || NextRefreshAttemptAtUtc.Value <= utcNow);
        }

        /// <summary>
        /// Регистрирует неудачную фоновую попытку и завершает обработку после достижения лимита.
        /// </summary>
        public bool TryRegisterRefreshFailure(
            string? errorMessage,
            DateTime attemptedAtUtc,
            DateTime nextAttemptAtUtc,
            int maxAttempts,
            out string? error)
        {
            if (!CanAttemptRefresh(attemptedAtUtc, maxAttempts))
            {
                error = "Фоновая попытка обновления чека сейчас недоступна.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(errorMessage))
            {
                error = "Причина ошибки обновления чека не указана.";
                return false;
            }

            if (nextAttemptAtUtc <= attemptedAtUtc)
            {
                error = "Дата следующей попытки должна быть позже текущей попытки.";
                return false;
            }

            string normalizedErrorMessage = errorMessage.Trim();
            RefreshAttemptCount++;
            LastRefreshAttemptAtUtc = attemptedAtUtc;
            LastRefreshError = normalizedErrorMessage[..Math.Min(normalizedErrorMessage.Length, MAX_REFRESH_ERROR_LENGTH)];

            if (RefreshAttemptCount >= maxAttempts)
            {
                RefreshStatus = ReceiptRefreshStatus.Failed;
                NextRefreshAttemptAtUtc = null;
            }
            else
            {
                RefreshStatus = ReceiptRefreshStatus.Pending;
                NextRefreshAttemptAtUtc = nextAttemptAtUtc;
            }

            error = null;
            return true;
        }

        /// <summary>
        /// Завершает обновление чека и при необходимости учитывает фоновую попытку.
        /// </summary>
        public bool TryCompleteRefresh(DateTime attemptedAtUtc, bool registerAttempt, out string? error)
        {
            if (attemptedAtUtc == default)
            {
                error = "Дата попытки обновления чека не заполнена.";
                return false;
            }

            if (registerAttempt && RefreshStatus != ReceiptRefreshStatus.Pending)
            {
                error = "Фоновую попытку можно завершить только для чека, ожидающего обновления.";
                return false;
            }

            if (registerAttempt)
                RefreshAttemptCount++;

            LastRefreshAttemptAtUtc = attemptedAtUtc;
            NextRefreshAttemptAtUtc = null;
            LastRefreshError = null;
            RefreshStatus = ReceiptRefreshStatus.Completed;
            error = null;
            return true;
        }

        /// <summary>
        /// Обновляет расширенные реквизиты чека после получения данных внешнего источника.
        /// </summary>
        public bool TryUpdateDetails(
            string? user,
            string? userInn,
            int? checkNumber,
            int? shiftNumber,
            TaxationType? taxationType,
            decimal cashTotalSum,
            decimal ecashTotalSum,
            decimal? nds18,
            decimal? nds10,
            decimal? nds0,
            decimal? ndsNo,
            string? kktRegId,
            string? numberKkt,
            string? region,
            out string? error)
        {
            string? normalizedUser = NormalizeOptional(user);
            string? normalizedUserInn = NormalizeOptional(userInn);
            string? normalizedKktRegId = NormalizeOptional(kktRegId);
            string? normalizedNumberKkt = NormalizeOptional(numberKkt);
            string? normalizedRegion = NormalizeOptional(region);

            if (!TryValidateDetails(
                    normalizedUser,
                    normalizedUserInn,
                    checkNumber,
                    shiftNumber,
                    taxationType,
                    cashTotalSum,
                    ecashTotalSum,
                    nds18,
                    nds10,
                    nds0,
                    ndsNo,
                    normalizedKktRegId,
                    normalizedNumberKkt,
                    normalizedRegion,
                    out error))
                return false;

            User = normalizedUser;
            UserInn = normalizedUserInn;
            CheckNumber = checkNumber;
            ShiftNumber = shiftNumber;
            TaxationType = taxationType;
            CashTotalSum = cashTotalSum;
            EcashTotalSum = ecashTotalSum;
            Nds18 = nds18;
            Nds10 = nds10;
            Nds0 = nds0;
            NdsNo = ndsNo;
            KktRegId = normalizedKktRegId;
            NumberKkt = normalizedNumberKkt;
            Region = normalizedRegion;
            return true;
        }

        /// <summary>
        /// Добавляет позицию в чек.
        /// </summary>
        public bool TryAddItem(ReceiptItem item, out string? error)
        {
            if (item == null)
            {
                error = "Позиция чека не указана.";
                return false;
            }

            if (_items.Contains(item))
            {
                error = "Позиция уже добавлена в чек.";
                return false;
            }

            if (!item.TryAttachTo(this, out error))
                return false;

            _items.Add(item);
            return true;
        }

        /// <summary>
        /// Назначает магазин, указанный в чеке.
        /// </summary>
        public void AssignStore(Store? store)
        {
            StoreId = store?.Id == Guid.Empty ? null : store?.Id;
            Store = store;
        }

        /// <summary>
        /// Добавляет связь чека со счётом.
        /// </summary>
        public bool TryAddAccount(Guid accountId, out ReceiptAccount? link, out string? error)
        {
            link = null;

            if (accountId == Guid.Empty)
            {
                error = "Некорректный идентификатор счёта.";
                return false;
            }

            if (_accounts.Any(item => item.AccountId == accountId))
            {
                error = "Чек уже привязан к выбранному счёту.";
                return false;
            }

            link = ReceiptAccount.Create(this, accountId);
            _accounts.Add(link);
            error = null;
            return true;
        }

        /// <summary>
        /// Добавляет связь чека с указанным счётом и согласованно задаёт навигацию.
        /// </summary>
        public bool TryAddAccount(Account account, out ReceiptAccount? link, out string? error)
        {
            link = null;

            if (account == null)
            {
                error = "Счёт не указан.";
                return false;
            }

            if (!TryAddAccount(account.Id, out link, out error))
                return false;

            link!.AttachAccount(account);
            return true;
        }

        internal bool TryAttachMoneyMovementLink(MoneyMovementReceipt link, out string? error)
        {
            if (link == null ||
                link.ReceiptId != Id ||
                !ReferenceEquals(link.Receipt, this) ||
                link.MoneyMovementId == Guid.Empty ||
                link.CreatedByUserId == Guid.Empty ||
                link.CreatedAtUtc == default)
            {
                error = "Некорректная связь чека с операцией.";
                return false;
            }

            if (_moneyMovementLinks.Any(item => item.MoneyMovementId == link.MoneyMovementId))
            {
                error = "Операция уже привязана к чеку.";
                return false;
            }

            _moneyMovementLinks.Add(link);
            error = null;
            return true;
        }

        /// <summary>
        /// Удаляет связь чека со счётом.
        /// </summary>
        public bool TryRemoveAccount(Guid accountId, out ReceiptAccount? link, out string? error)
        {
            link = null;

            if (accountId == Guid.Empty)
            {
                error = "Некорректный идентификатор счёта.";
                return false;
            }

            link = _accounts.FirstOrDefault(item => item.AccountId == accountId);
            if (link == null)
            {
                error = "Чек не привязан к выбранному счёту.";
                return false;
            }

            _accounts.Remove(link);
            error = null;
            return true;
        }

        /// <summary>
        /// Переносит связь чека с одного счёта на другой.
        /// </summary>
        public bool TryMoveAccount(
            Guid sourceAccountId,
            Guid targetAccountId,
            out ReceiptAccount? removedLink,
            out ReceiptAccount? addedLink,
            out string? error)
        {
            removedLink = null;
            addedLink = null;

            if (targetAccountId == Guid.Empty)
            {
                error = "Некорректный идентификатор счёта назначения.";
                return false;
            }

            if (sourceAccountId != Guid.Empty && sourceAccountId == targetAccountId)
            {
                error = "Счёт назначения должен отличаться от исходного счёта.";
                return false;
            }

            removedLink = sourceAccountId == Guid.Empty
                ? null
                : _accounts.FirstOrDefault(item => item.AccountId == sourceAccountId);
            if (sourceAccountId != Guid.Empty && removedLink == null)
            {
                error = "Чек не привязан к выбранному исходному счёту.";
                return false;
            }

            if (_accounts.Any(item => item.AccountId == targetAccountId))
            {
                error = "Чек уже привязан к счёту назначения.";
                return false;
            }

            addedLink = ReceiptAccount.Create(this, targetAccountId);

            if (removedLink != null)
                _accounts.Remove(removedLink);

            _accounts.Add(addedLink);
            error = null;
            return true;
        }

        /// <summary>
        /// Возвращает доменный ключ идентичности чека.
        /// </summary>
        public string GetIdentityKey()
        {
            return string.Join('|',
                FiscalDriveNumber,
                FiscalDocumentNumber,
                FiscalSign,
                DateTime.Ticks,
                TotalSum,
                (int)OperationType);
        }

        /// <summary>
        /// Обновляет данные чека из другой доменной модели.
        /// </summary>
        public bool TryRefreshFrom(
            Receipt source,
            Store? store,
            IEnumerable<ReceiptItem> items,
            DateTime updatedAtUtc,
            out string? error)
        {
            if (source == null)
            {
                error = "Не указаны обновлённые данные чека.";
                return false;
            }

            if (updatedAtUtc == default)
            {
                error = "Дата обновления чека не заполнена.";
                return false;
            }

            if (!TryNormalizeIdentity(
                    source.FiscalDriveNumber,
                    source.FiscalDocumentNumber,
                    source.FiscalSign,
                    out string fiscalDriveNumber,
                    out string fiscalDocumentNumber,
                    out string fiscalSign,
                    out error))
                return false;

            if (source.DateTime == default || !Enum.IsDefined(source.OperationType) || source.TotalSum <= 0)
            {
                error = "Внешний источник вернул некорректные обязательные реквизиты чека.";
                return false;
            }

            string? normalizedUser = NormalizeOptional(source.User);
            string? normalizedUserInn = NormalizeOptional(source.UserInn);
            string? normalizedKktRegId = NormalizeOptional(source.KktRegId);
            string? normalizedNumberKkt = NormalizeOptional(source.NumberKkt);
            string? normalizedRegion = NormalizeOptional(source.Region);

            if (!TryValidateDetails(
                    normalizedUser,
                    normalizedUserInn,
                    source.CheckNumber,
                    source.ShiftNumber,
                    source.TaxationType,
                    source.CashTotalSum,
                    source.EcashTotalSum,
                    source.Nds18,
                    source.Nds10,
                    source.Nds0,
                    source.NdsNo,
                    normalizedKktRegId,
                    normalizedNumberKkt,
                    normalizedRegion,
                    out error))
                return false;

            List<ReceiptItem> refreshedItems = items?.ToList() ?? new List<ReceiptItem>();
            foreach (ReceiptItem item in refreshedItems)
            {
                if (!item.CanAttachTo(this, out error))
                    return false;
            }

            FiscalDriveNumber = fiscalDriveNumber;
            FiscalDocumentNumber = fiscalDocumentNumber;
            FiscalSign = fiscalSign;
            StoreId = store?.Id == Guid.Empty ? null : store?.Id;
            Store = store;
            User = normalizedUser;
            UserInn = normalizedUserInn;
            DateTime = source.DateTime;
            CheckNumber = source.CheckNumber;
            ShiftNumber = source.ShiftNumber;
            OperationType = source.OperationType;
            TaxationType = source.TaxationType;
            TotalSum = source.TotalSum;
            CashTotalSum = source.CashTotalSum;
            EcashTotalSum = source.EcashTotalSum;
            Nds18 = source.Nds18;
            Nds10 = source.Nds10;
            Nds0 = source.Nds0;
            NdsNo = source.NdsNo;
            KktRegId = normalizedKktRegId;
            NumberKkt = normalizedNumberKkt;
            Region = normalizedRegion;
            UpdatedAtUtc = updatedAtUtc;

            _items.Clear();
            foreach (ReceiptItem item in refreshedItems)
            {
                if (!item.TryAttachTo(this, out error))
                    return false;

                _items.Add(item);
            }

            error = null;
            return true;
        }

        private static bool TryNormalizeIdentity(
            string? fiscalDriveNumber,
            string? fiscalDocumentNumber,
            string? fiscalSign,
            out string normalizedFiscalDriveNumber,
            out string normalizedFiscalDocumentNumber,
            out string normalizedFiscalSign,
            out string? error)
        {
            normalizedFiscalDriveNumber = fiscalDriveNumber?.Trim() ?? string.Empty;
            normalizedFiscalDocumentNumber = fiscalDocumentNumber?.Trim() ?? string.Empty;
            normalizedFiscalSign = fiscalSign?.Trim() ?? string.Empty;

            if (normalizedFiscalDriveNumber.Length == 0 ||
                normalizedFiscalDocumentNumber.Length == 0 ||
                normalizedFiscalSign.Length == 0)
            {
                error = "Фискальные реквизиты чека обязательны.";
                return false;
            }

            if (normalizedFiscalDriveNumber.Length > MAX_FISCAL_NUMBER_LENGTH ||
                normalizedFiscalDocumentNumber.Length > MAX_FISCAL_NUMBER_LENGTH ||
                normalizedFiscalSign.Length > MAX_FISCAL_NUMBER_LENGTH)
            {
                error = $"Фискальные реквизиты чека не должны превышать {MAX_FISCAL_NUMBER_LENGTH} символов.";
                return false;
            }

            error = null;
            return true;
        }

        private static bool TryValidateDetails(
            string? user,
            string? userInn,
            int? checkNumber,
            int? shiftNumber,
            TaxationType? taxationType,
            decimal cashTotalSum,
            decimal ecashTotalSum,
            decimal? nds18,
            decimal? nds10,
            decimal? nds0,
            decimal? ndsNo,
            string? kktRegId,
            string? numberKkt,
            string? region,
            out string? error)
        {
            if (user?.Length > MAX_USER_LENGTH ||
                userInn?.Length > MAX_USER_INN_LENGTH ||
                kktRegId?.Length > MAX_KKT_ID_LENGTH ||
                numberKkt?.Length > MAX_KKT_ID_LENGTH ||
                region?.Length > MAX_REGION_LENGTH)
            {
                error = "Текстовые реквизиты чека превышают допустимую длину.";
                return false;
            }

            if (checkNumber < 0 || shiftNumber < 0)
            {
                error = "Номер чека или смены не может быть отрицательным.";
                return false;
            }

            if (taxationType.HasValue && !Enum.IsDefined(taxationType.Value))
            {
                error = "Некорректная система налогообложения.";
                return false;
            }

            if (cashTotalSum < 0 ||
                ecashTotalSum < 0 ||
                nds18 < 0 ||
                nds10 < 0 ||
                nds0 < 0 ||
                ndsNo < 0)
            {
                error = "Денежные суммы чека не могут быть отрицательными.";
                return false;
            }

            error = null;
            return true;
        }

        private static string? NormalizeOptional(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
