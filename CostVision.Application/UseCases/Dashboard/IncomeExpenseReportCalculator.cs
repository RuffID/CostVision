using System.Globalization;
using CostVision.Application.Extensions;
using CostVision.Application.Models.Dtos.Dashboard;
using CostVision.Domain.Models.Enums.MoneyMovements;
using CostVision.Domain.Models.Receipts;

namespace CostVision.Application.UseCases.Dashboard;

internal record ReportReceipt(ReceiptIdentity Identity, IReadOnlyCollection<Guid> MovementIds);
internal record ReportMoneyMovement(Guid Id, DateTime Date, decimal Amount, MoneyMovementType Type);

// Входы уже ограничены доступом, периодом, счетами и магазинами. Компонент не читает и не меняет сущности.
internal static class IncomeExpenseReportCalculator
{
    public static List<DashboardIncomeExpensePointDto> Calculate(
        IReadOnlyList<ReportReceipt> receipts, IReadOnlyList<ReportMoneyMovement> movements,
        DateTime dateFrom, DateTime dateTo, string period, string source)
    {
        Dictionary<DateTime, decimal> income = new();
        Dictionary<DateTime, decimal> expense = new();
        if (source == "receipts")
        {
            foreach (ReportReceipt receipt in receipts)
            {
                AddReceipt(income, expense, receipt.Identity, receipt.Identity.TotalSum, period);
            }
        }
        else
        {
            foreach (ReportMoneyMovement movement in movements)
            {
                AddByDirection(income, expense, movement.Type, movement.Date, movement.Amount, period);
            }

            if (source == "receiptsAndMoneyMovements")
            {
                foreach (MoneyMovementType direction in new[] { MoneyMovementType.Expense, MoneyMovementType.Income })
                {
                    List<ReportReceipt> documents = receipts
                        .Where(receipt => ReceiptFinancialRules.GetDirection(receipt.Identity.OperationType) == direction)
                        .OrderBy(receipt => receipt.Identity.DateTime)
                        .ThenBy(receipt => receipt.Identity.ToInvariantKey(), StringComparer.Ordinal).ToList();
                    List<ReportMoneyMovement> payments = movements.Where(movement => movement.Type == direction)
                        .OrderBy(movement => movement.Date).ThenBy(movement => movement.Id).ToList();
                    decimal[] remaining = ReceiptPaymentCoverage.CalculateRemaining(documents, payments);
                    for (int index = 0; index < documents.Count; index++)
                    {
                        AddReceipt(income, expense, documents[index].Identity, remaining[index], period);
                    }
                }
            }
        }

        List<DashboardIncomeExpensePointDto> points = [];
        DateTime cursor = GetPeriodStart(dateFrom, period);
        DateTime end = GetPeriodStart(dateTo, period);
        CultureInfo culture = CultureInfo.GetCultureInfo("ru-RU");
        while (cursor <= end)
        {
            string label;
            DateTime next;
            if (period == "month")
            {
                label = cursor.ToString("MMMM yyyy", culture);
                next = cursor.AddMonths(1);
            }
            else
            {
                label = cursor.ToString("dd.MM", culture);
                next = cursor.AddDays(1);
            }

            points.Add(new DashboardIncomeExpensePointDto
            {
                PeriodStart = cursor, Label = label,
                IncomeSum = income.GetValueOrDefault(cursor), ExpenseSum = expense.GetValueOrDefault(cursor)
            });
            cursor = next;
        }

        return points;
    }

    private static void AddReceipt(Dictionary<DateTime, decimal> income, Dictionary<DateTime, decimal> expense,
        ReceiptIdentity identity, decimal amount, string period)
    {
        AddByDirection(income, expense, ReceiptFinancialRules.GetDirection(identity.OperationType), identity.DateTime, amount, period);
    }

    private static void AddByDirection(Dictionary<DateTime, decimal> income, Dictionary<DateTime, decimal> expense,
        MoneyMovementType direction, DateTime date, decimal amount, string period)
    {
        Dictionary<DateTime, decimal> amounts;
        if (direction == MoneyMovementType.Income)
        {
            amounts = income;
        }
        else if (direction == MoneyMovementType.Expense)
        {
            amounts = expense;
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(direction));
        }

        DateTime groupStart = GetPeriodStart(date, period);
        amounts[groupStart] = amounts.GetValueOrDefault(groupStart) + amount;
    }

    private static DateTime GetPeriodStart(DateTime date, string period)
    {
        if (period == "month")
        {
            return new DateTime(date.Year, date.Month, 1);
        }

        return date.Date;
    }
}
