namespace CostVision.Models.Enums.Receipts
{
    public enum ReceiptOperationType
    {
        Unknown = 0,
        Income = 1,          // Приход
        RefundIncome = 2,    // Возврат прихода
        Expense = 3,         // Расход
        RefundExpense = 4    // Возврат расхода
    }
}