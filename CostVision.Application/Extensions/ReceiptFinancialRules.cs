using CostVision.Domain.Models.Enums.MoneyMovements;
using CostVision.Domain.Models.Enums.Receipts;
using CostVision.Domain.Models.Receipts;
using System.Linq.Expressions;

namespace CostVision.Application.Extensions;

internal static class ReceiptFinancialRules
{
    private const decimal QUANTITY_UNIT_SCALE = 1000m;

    // SQL-проекции используют те же единицы, что и расчёт по загруженным значениям.
    public static Expression<Func<ReceiptItem, decimal>> NormalizedItemQuantity { get; } = item =>
        item.ItemsQuantityMeasure == QuantityMeasureType.Gram ? item.Quantity / QUANTITY_UNIT_SCALE :
        item.ItemsQuantityMeasure == QuantityMeasureType.Ton ? item.Quantity * QUANTITY_UNIT_SCALE : item.Quantity;

    public static Expression<Func<ReceiptItem, bool>> WeightedItem { get; } = item =>
        item.ItemsQuantityMeasure == QuantityMeasureType.Gram || item.ItemsQuantityMeasure == QuantityMeasureType.Kilogram ||
        item.ItemsQuantityMeasure == QuantityMeasureType.Ton;

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
                return quantity / QUANTITY_UNIT_SCALE;
            case QuantityMeasureType.Ton:
                return quantity * QUANTITY_UNIT_SCALE;
            default:
                return quantity;
        }
    }

    public static bool IsWeighted(QuantityMeasureType measure)
    {
        return measure is QuantityMeasureType.Gram or QuantityMeasureType.Kilogram or QuantityMeasureType.Ton;
    }
}
