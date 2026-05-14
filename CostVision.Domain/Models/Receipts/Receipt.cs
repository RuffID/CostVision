using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.Enums.Receipts;
using EFCoreLibrary.Abstractions.Entity;

namespace CostVision.Domain.Models.Receipts
{
    public class Receipt : IEntity<Guid>
    {
        public Guid Id { get; set; }

        /// <summary>
        /// ФН (FN) - номер фискального накопителя.
        /// </summary>
        public string FiscalDriveNumber { get; set; } = string.Empty;

        /// <summary>
        /// ФД (FD) - фискальный номер документа.
        /// </summary>
        public string FiscalDocumentNumber { get; set; } = string.Empty;

        /// <summary>
        /// ФПД, ФП (FP) - фискальный признак документа.
        /// </summary>
        public string FiscalSign { get; set; } = string.Empty;

        public string? RetailPlace { get; set; }

        public string? RetailPlaceAddress { get; set; }

        /// <summary>
        /// Наименование юридического лица.
        /// </summary>
        public string? User { get; set; }

        public string? UserInn { get; set; }

        /// <summary>
        /// Время покупки.
        /// </summary>
        public DateTime DateTime { get; set; }

        /// <summary>
        /// Номер чека за смену.
        /// </summary>
        public int? CheckNumber { get; set; }

        public int? ShiftNumber { get; set; }

        public ReceiptOperationType OperationType { get; set; }

        public TaxationType? TaxationType { get; set; }

        public decimal TotalSum { get; set; }

        public decimal CashTotalSum { get; set; }

        public decimal EcashTotalSum { get; set; }

        /// <summary>
        /// Налоги.
        /// </summary>
        public decimal? Nds18 { get; set; }

        public decimal? Nds10 { get; set; }

        public decimal? Nds0 { get; set; }

        public decimal? NdsNo { get; set; }

        /// <summary>
        /// Информация о кассовой технике.
        /// </summary>
        public string? KktRegId { get; set; }

        public string? NumberKkt { get; set; }

        /// <summary>
        /// Регион или город покупки.
        /// </summary>
        public string? Region { get; set; }

        /// <summary>
        /// Идентификатор пользователя, который импортировал чек.
        /// </summary>
        public Guid CreatedByUserId { get; set; }

        public User? CreatedByUser { get; set; }

        /// <summary>
        /// Позиции чека.
        /// </summary>
        public List<ReceiptItem> Items { get; set; } = new();

        /// <summary>
        /// Связи чека со счетами.
        /// </summary>
        public List<ReceiptAccount> Accounts { get; set; } = new();

        /// <summary>
        /// Метка времени создания записи в системе.
        /// </summary>
        public DateTime CreatedAtUtc { get; set; }

        public DateTime? UpdatedAtUtc { get; set; }

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
        public void ApplyDetailsFrom(Receipt source)
        {
            FiscalDriveNumber = source.FiscalDriveNumber;
            FiscalDocumentNumber = source.FiscalDocumentNumber;
            FiscalSign = source.FiscalSign;
            RetailPlace = source.RetailPlace;
            RetailPlaceAddress = source.RetailPlaceAddress;
            User = source.User;
            UserInn = source.UserInn;
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
            KktRegId = source.KktRegId;
            NumberKkt = source.NumberKkt;
            Region = source.Region;
        }

        /// <summary>
        /// Отмечает момент обновления чека.
        /// </summary>
        public void MarkUpdated(DateTime updatedAtUtc)
        {
            UpdatedAtUtc = updatedAtUtc;
        }
    }
}
