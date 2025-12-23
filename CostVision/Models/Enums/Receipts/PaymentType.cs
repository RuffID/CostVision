namespace CostVision.Models.Enums.Receipts
{
    public enum PaymentType
    {
        // 1 — Полная предварительная оплата до передачи
        FullPrepayment = 1,

        // 2 — Частичная предварительная оплата до передачи
        PartialPrepayment = 2,

        // 3 — Аванс
        Advance = 3,

        // 4 — Полная оплата в момент передачи
        FullPayment = 4,

        // 5 — Частичная оплата в момент передачи + кредит
        PartialPaymentAndCredit = 5,

        // 6 — Передача товара в кредит (без оплаты в момент передачи)
        CreditTransfer = 6,

        // 7 — Оплата кредита (постоплата)
        CreditPayment = 7
    }
}
