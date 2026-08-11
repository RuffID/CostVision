namespace CostVision.Domain.Models.Enums.Receipts
{
    /// <summary>
    /// Признак способа расчёта (тег 1214 ФФД).
    /// </summary>
    public enum PaymentType
    {
        FullPrepayment = 1,
        PartialPrepayment = 2,
        Advance = 3,
        FullPayment = 4,
        PartialPaymentAndCredit = 5,
        Credit = 6,
        CreditPayment = 7
    }
}
