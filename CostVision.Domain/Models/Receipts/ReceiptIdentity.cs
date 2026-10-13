using CostVision.Domain.Models.Enums.Receipts;
using System.Globalization;

namespace CostVision.Domain.Models.Receipts;

/// <summary>
/// Идентичность фискального документа, независимо от автора копии и связей со счетами.
/// </summary>
public record ReceiptIdentity
{
    internal ReceiptIdentity(Receipt receipt)
    {
        FiscalDriveNumber = receipt.FiscalDriveNumber;
        FiscalDocumentNumber = receipt.FiscalDocumentNumber;
        FiscalSign = receipt.FiscalSign;
        DateTime = receipt.DateTime;
        TotalSum = receipt.TotalSum;
        OperationType = receipt.OperationType;
    }

    public string FiscalDriveNumber { get; }
    public string FiscalDocumentNumber { get; }
    public string FiscalSign { get; }
    public DateTime DateTime { get; }
    public decimal TotalSum { get; }
    public ReceiptOperationType OperationType { get; }

    /// <summary>
    /// Возвращает строковое представление для прежних потребителей без зависимости от культуры.
    /// </summary>
    public string ToInvariantKey()
    {
        return string.Join('|',
            FiscalDriveNumber,
            FiscalDocumentNumber,
            FiscalSign,
            DateTime.Ticks.ToString(CultureInfo.InvariantCulture),
            TotalSum.ToString("G29", CultureInfo.InvariantCulture),
            ((int)OperationType).ToString(CultureInfo.InvariantCulture));
    }
}
