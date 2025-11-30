using CostVision.Interfaces.Entity;
using CostVision.Models.Authorization;
using CostVision.Models.Enums.Document;

namespace CostVision.Models.Receipts
{
    public class Receipt : IEntity<Guid>, ICopyable<Receipt>
    {
        public Guid Id { get; set; }

        // Номер фискального накопителя
        public string FiscalDriveNumber { get; set; } = string.Empty;
        // Фискальный номер документа
        public string FiscalDocumentNumber { get; set; } = string.Empty;
        // Фискальный признак документа
        public string FiscalSign { get; set; } = string.Empty;
        public string? RetailPlace { get; set; }
        public string? RetailPlaceAddress { get; set; }
        // Наименование юрлица
        public string? User { get; set; }
        public string? UserInn { get; set; }

        // Время покупки
        public DateTime DateTime { get; set; }
        // Номер чека за смену
        public int? CheckNumber { get; set; }
        public int? ShiftNumber { get; set; }

        public ReceiptOperationType OperationType { get; set; }
        public TaxationType? TaxationType { get; set; }

        public decimal TotalSum { get; set; }
        public decimal CashTotalSum { get; set; }
        public decimal EcashTotalSum { get; set; }
        // Налоги
        public decimal? Nds18 { get; set; }
        public decimal? Nds10 { get; set; }
        public decimal? Nds0 { get; set; }
        public decimal? NdsNo { get; set; }

        // Информация о ККТ
        public string? KktRegId { get; set; }
        public string? NumberKkt { get; set; }
        // Регион (город) покупки
        public string? Region { get; set; }

        // Кто загрузил чек (владелец импорта)
        public Guid CreatedByUserId { get; set; }
        public virtual User? CreatedByUser { get; set; }

        // Товары в чеке
        public virtual List<ReceiptItem> Items { get; set; } = new ();
        // Много счетов у одного чека
        public virtual List<ReceiptAccount> Accounts { get; set; } = new();

        // Метаданные для аналитики (этой информации нет в чеке)
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
