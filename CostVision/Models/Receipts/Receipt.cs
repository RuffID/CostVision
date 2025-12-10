using CostVision.Interfaces.Entity;
using CostVision.Models.Authorization;
using CostVision.Models.Enums.Document;

namespace CostVision.Models.Receipts
{
    public class Receipt : IEntity<Guid>, ICopyable<Receipt>
    {
        public Guid Id { get; set; }

        /// <summary>
        /// ФН (FN) - номер фискального накопителя
        /// </summary>
        public string FiscalDriveNumber { get; set; } = string.Empty;
        /// <summary>
        /// ФД (FD) - Фискальный номер документа 
        /// </summary>
        public string FiscalDocumentNumber { get; set; } = string.Empty;
        /// <summary>
        /// ФПД, ФП (FP) - Фискальный признак документа
        /// </summary>
        public string FiscalSign { get; set; } = string.Empty;
        public string? RetailPlace { get; set; }
        public string? RetailPlaceAddress { get; set; }
        /// <summary>
        /// Наименование юрлица
        /// </summary>
        public string? User { get; set; }
        public string? UserInn { get; set; }

        /// <summary>
        /// Время покупки
        /// </summary>
        public DateTime DateTime { get; set; }
        /// <summary>
        /// Номер чека за смену
        /// </summary>
        public int? CheckNumber { get; set; }
        public int? ShiftNumber { get; set; }

        public ReceiptOperationType OperationType { get; set; }
        public TaxationType? TaxationType { get; set; }

        public decimal TotalSum { get; set; }
        public decimal CashTotalSum { get; set; }
        public decimal EcashTotalSum { get; set; }
        /// <summary>
        /// Налоги
        /// </summary>
        public decimal? Nds18 { get; set; }
        public decimal? Nds10 { get; set; }
        public decimal? Nds0 { get; set; }
        public decimal? NdsNo { get; set; }

        /// <summary>
        /// Информация о ККТ
        /// </summary>
        public string? KktRegId { get; set; }
        public string? NumberKkt { get; set; }
        /// <summary>
        /// Регион (город) покупки
        /// </summary>
        public string? Region { get; set; }

        /// <summary>
        /// Кто загрузил чек (владелец импорта)
        /// </summary>
        public Guid CreatedByUserId { get; set; }
        public virtual User? CreatedByUser { get; set; }

        /// <summary>
        /// Товары в чеке
        /// </summary>
        public virtual List<ReceiptItem> Items { get; set; } = new ();
        /// <summary>
        /// Много счетов у одного чека
        /// </summary>
        public virtual List<ReceiptAccount> Accounts { get; set; } = new();

        /// <summary>
        /// Метаданные для аналитики (этой информации нет в чеке)
        /// </summary>
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? UpdatedAtUtc { get; set; }

        public void CopyData(Receipt entity)
        {
            FiscalDriveNumber = entity.FiscalDriveNumber;
            FiscalDocumentNumber = entity.FiscalDocumentNumber;
            FiscalSign = entity.FiscalSign;
            RetailPlace = entity.RetailPlace;
            RetailPlaceAddress = entity.RetailPlaceAddress;
            User = entity.User;
            UserInn = entity.UserInn;
            DateTime = entity.DateTime;
            CheckNumber = entity.CheckNumber;
            ShiftNumber = entity.ShiftNumber;
            OperationType = entity.OperationType;
            TaxationType = entity.TaxationType;
            TotalSum = entity.TotalSum;
            CashTotalSum = entity.CashTotalSum;
            Nds18 = entity.Nds18;
            Nds10 = entity.Nds10;
            Nds0 = entity.Nds0;
            NdsNo = entity.NdsNo;
            KktRegId = entity.KktRegId;
            NumberKkt = entity.NumberKkt;
            Region = entity.Region;
        }
    }
}
