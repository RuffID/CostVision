using CostVision.Models.Enums.Document;

namespace CostVision.Models.Responses.ProverkachekaApi
{
    public class ProverkachekaJson
    {
        public int Code { get; set; }

        // Юрлицо
        public string? User { get; set; }

        // ИНН
        public string? UserInn { get; set; }

        // Регион
        public string? Region { get; set; }

        // Торговая точка
        public string? RetailPlace { get; set; }

        // Адрес торговой точки
        public string? RetailPlaceAddress { get; set; }

        // Дата/время чека
        public DateTime DateTime { get; set; }

        // Номер смены
        public int ShiftNumber { get; set; }

        // Номер чека
        public int RequestNumber { get; set; }

        public ReceiptOperationType OperationType { get; set; }

        // Налогообложение
        public TaxationType AppliedTaxationType { get; set; }

        // Общая сумма в копейках
        public int TotalSum { get; set; }

        // Наличная сумма в копейках
        public int CashTotalSum { get; set; }

        // Безналичная сумма в копейках
        public int EcashTotalSum { get; set; }

        // НДС 10% (копейки)
        public int? Nds10 { get; set; }

        // НДС 20% (копейки)
        public int? Nds18 { get; set; }

        // НДС 0% (копейки)
        public int? Nds0 { get; set; }

        // НДС без налогообложения (копейки)
        public int? NdsNo { get; set; }

        // Номер регистрации ККТ
        public string? KktRegId { get; set; }

        // Номер ККТ
        public string? NumberKkt { get; set; }

        // Номер фискального накопителя
        public string? FiscalDriveNumber { get; set; }

        // Номер фискального документа
        public int FiscalDocumentNumber { get; set; }

        // Фискальный признак документа
        public long FiscalSign { get; set; }

        // URL ФНС
        public string? FnsUrl { get; set; }

        // Номер машины (часто = номер ККТ)
        public string? MachineNumber { get; set; }

        // Формат фискального документа
        public int FiscalDocumentFormatVer { get; set; }

        // Результат проверки маркированной продукции
        public int? CheckingLabeledProdResult { get; set; }

        // Дополнительные свойства
        public ProverkachekaProperties? Properties { get; set; }

        // Metadata (ofdId, address, receiveDate)
        public ProverkachekaMetadata? Metadata { get; set; }

        // Позиции чека
        public List<ProverkachekaItem> Items { get; set; } = new List<ProverkachekaItem>();
    }
}