using CostVision.Domain.Models.Enums.MoneyMovements;
using CostVision.Domain.Models.Enums.Receipts;

namespace CostVision.Application.Extensions;

internal static class ReceiptFinancialRules
{
    public static (decimal Income, decimal Expense) CalculateTotals(IEnumerable<(ReceiptOperationType Operation, decimal Amount)> documents)
    {
        decimal income = 0m;
        decimal expense = 0m;
        foreach (var document in documents)
        {
            if (GetDirection(document.Operation) == MoneyMovementType.Income)
            {
                income += document.Amount;
            }
            else
            {
                expense += document.Amount;
            }
        }

        return (income, expense);
    }

    public static MoneyMovementType GetDirection(ReceiptOperationType operation)
    {
        switch (operation)
        {
            case ReceiptOperationType.Income:
            case ReceiptOperationType.RefundExpense:
                return MoneyMovementType.Expense;
            case ReceiptOperationType.RefundIncome:
            case ReceiptOperationType.Expense:
                return MoneyMovementType.Income;
            default:
                throw new ArgumentOutOfRangeException(nameof(operation), operation, "Неизвестный вид фискальной операции.");
        }
    }

    public static decimal NormalizeQuantity(decimal quantity, QuantityMeasureType measure)
    {
        switch (measure)
        {
            case QuantityMeasureType.Gram:
                return quantity / 1000m;
            case QuantityMeasureType.Ton:
                return quantity * 1000m;
            default:
                return quantity;
        }
    }

    public static bool IsWeighted(QuantityMeasureType measure)
    {
        return measure is QuantityMeasureType.Gram or QuantityMeasureType.Kilogram or QuantityMeasureType.Ton;
    }
}
