using System.Globalization;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Models.Dtos.Dashboard;
using CostVision.Application.Models.Requests.Dashboard;
using CostVision.Application.Models.Responses.Results;
using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.Enums.MoneyMovements;
using CostVision.Domain.Models.MoneyMovements;
using CostVision.Domain.Models.Receipts;
using Microsoft.EntityFrameworkCore;

namespace CostVision.Application.UseCases.Dashboard
{
    public class GetDashboardIncomeExpenseReportUseCase(IUnitOfWork unitOfWork) : IGetDashboardIncomeExpenseReportUseCase
    {
        private const string PERIOD_DAY = "day";
        private const string PERIOD_MONTH = "month";
        private const string EXPENSE_SOURCE_RECEIPTS = "receipts";
        private const string EXPENSE_SOURCE_MONEY_MOVEMENTS = "moneyMovements";
        private const string EXPENSE_SOURCE_RECEIPTS_AND_MONEY_MOVEMENTS = "receiptsAndMoneyMovements";
        private const string UNKNOWN_STORE_NAME = "Без магазина";

        public async Task<ServiceResult<DashboardIncomeExpenseReportDto>> ExecuteAsync(User currentUser, DashboardIncomeExpenseReportRequest request, CancellationToken ct)
        {
            if (request.DateTo.Date < request.DateFrom.Date)
                return ServiceResult<DashboardIncomeExpenseReportDto>.Fail(400, "Дата окончания периода не может быть меньше даты начала.");

            if (!IsSupportedPeriod(request.Period))
                return ServiceResult<DashboardIncomeExpenseReportDto>.Fail(400, "Некорректная группировка отчёта.");

            if (!IsSupportedExpenseSource(request.ExpenseSource))
                return ServiceResult<DashboardIncomeExpenseReportDto>.Fail(400, "Некорректный источник расходов.");

            string period = request.Period;
            string expenseSource = request.ExpenseSource;
            DateTime periodStart = request.DateFrom.Date;
            DateTime periodEnd = request.DateTo.Date.AddDays(1);
            HashSet<Guid> selectedAccountIds = request.AccountIds
                .Where(accountId => accountId != Guid.Empty)
                .ToHashSet();
            bool hasAccountFilter = selectedAccountIds.Count > 0 || request.IncludeWithoutAccount;

            List<Receipt> receipts = await unitOfWork.Receipt.GetItemsByPredicateAsync(
                receipt => receipt.DateTime >= periodStart &&
                           receipt.DateTime < periodEnd &&
                           (!hasAccountFilter ||
                            receipt.Accounts.Any(link => selectedAccountIds.Contains(link.AccountId)) ||
                            (request.IncludeWithoutAccount && !receipt.Accounts.Any())) &&
                           (receipt.CreatedByUserId == currentUser.Id ||
                            receipt.Accounts.Any(link => link.Account!.CreatedByUserId == currentUser.Id) ||
                            receipt.Accounts.Any(link => link.Account!.Members.Any(member => member.UserId == currentUser.Id))),
                asNoTracking: true,
                include: query => query
                    .Include(receipt => receipt.Store)
                    .Include(receipt => receipt.Accounts)
                        .ThenInclude(link => link.Account)
                            .ThenInclude(account => account!.Members)
                    .Include(receipt => receipt.MoneyMovementLinks)
                        .ThenInclude(link => link.MoneyMovement)
                    .AsSplitQuery(),
                ct: ct);

            List<MoneyMovement> moneyMovements = await unitOfWork.MoneyMovement.GetItemsByPredicateAsync(
                movement => movement.OccurredAt >= periodStart &&
                            movement.OccurredAt < periodEnd &&
                            (!hasAccountFilter || selectedAccountIds.Contains(movement.AccountId)) &&
                            (movement.Account!.CreatedByUserId == currentUser.Id ||
                             movement.Account.Members.Any(member => member.UserId == currentUser.Id)),
                asNoTracking: true,
                include: query => query
                    .Include(movement => movement.Account)
                        .ThenInclude(account => account!.Members)
                    .Include(movement => movement.ReceiptLinks)
                        .ThenInclude(link => link.Receipt)
                            .ThenInclude(receipt => receipt!.Store)
                    .AsSplitQuery(),
                ct: ct);

            List<string> stores = receipts
                .Select(GetStoreName)
                .Concat(moneyMovements
                    .SelectMany(movement => movement.ReceiptLinks)
                    .Where(link => link.Receipt != null)
                    .Select(link => GetStoreName(link.Receipt!)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(store => store)
                .ToList();

            HashSet<string> selectedStores = request.StoreNames
                .Where(store => !string.IsNullOrWhiteSpace(store))
                .Select(store => store.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            List<Receipt> filteredReceipts = selectedStores.Count == 0
                ? receipts
                : receipts.Where(receipt => selectedStores.Contains(GetStoreName(receipt))).ToList();

            List<MoneyMovement> filteredMoneyMovements = selectedStores.Count == 0
                ? moneyMovements
                : moneyMovements
                    .Where(movement => movement.ReceiptLinks
                        .Any(link => link.Receipt != null && selectedStores.Contains(GetStoreName(link.Receipt!))))
                    .ToList();

            Dictionary<DateTime, decimal> incomeAmounts = BuildIncomeAmounts(moneyMovements, period);
            Dictionary<DateTime, decimal> expenseAmounts = BuildExpenseAmounts(filteredReceipts, filteredMoneyMovements, expenseSource, periodStart, periodEnd, period);
            List<DashboardIncomeExpensePointDto> points = BuildPoints(incomeAmounts, expenseAmounts, periodStart, request.DateTo.Date, period);

            return ServiceResult<DashboardIncomeExpenseReportDto>.Ok(new DashboardIncomeExpenseReportDto
            {
                Stores = stores,
                Points = points
            });
        }

        private static bool IsSupportedPeriod(string period)
        {
            return period switch
            {
                PERIOD_DAY => true,
                PERIOD_MONTH => true,
                _ => false
            };
        }

        private static bool IsSupportedExpenseSource(string expenseSource)
        {
            return expenseSource switch
            {
                EXPENSE_SOURCE_RECEIPTS => true,
                EXPENSE_SOURCE_MONEY_MOVEMENTS => true,
                EXPENSE_SOURCE_RECEIPTS_AND_MONEY_MOVEMENTS => true,
                _ => false
            };
        }

        private static Dictionary<DateTime, decimal> BuildIncomeAmounts(List<MoneyMovement> moneyMovements, string period)
        {
            Dictionary<DateTime, decimal> amounts = new();

            foreach (MoneyMovement movement in moneyMovements)
            {
                if (movement.Type == MoneyMovementType.Income)
                    AddAmount(amounts, movement.OccurredAt, movement.Amount, period);
            }

            return amounts;
        }

        private static Dictionary<DateTime, decimal> BuildExpenseAmounts(
            List<Receipt> receipts,
            List<MoneyMovement> moneyMovements,
            string expenseSource,
            DateTime periodStart,
            DateTime periodEnd,
            string period)
        {
            Dictionary<DateTime, decimal> amounts = new();

            if (expenseSource == EXPENSE_SOURCE_RECEIPTS)
            {
                AddReceiptAmounts(amounts, receipts, period);
                return amounts;
            }

            foreach (MoneyMovement movement in moneyMovements)
            {
                if (movement.Type == MoneyMovementType.Expense)
                    AddAmount(amounts, movement.OccurredAt, movement.Amount, period);
            }

            if (expenseSource == EXPENSE_SOURCE_MONEY_MOVEMENTS)
                return amounts;

            foreach (Receipt receipt in receipts)
            {
                if (!HasExpenseMoneyMovementInPeriod(receipt, periodStart, periodEnd))
                    AddAmount(amounts, receipt.DateTime, receipt.TotalSum, period);
            }

            return amounts;
        }

        private static void AddReceiptAmounts(Dictionary<DateTime, decimal> amounts, List<Receipt> receipts, string period)
        {
            foreach (Receipt receipt in receipts)
                AddAmount(amounts, receipt.DateTime, receipt.TotalSum, period);
        }

        private static void AddAmount(Dictionary<DateTime, decimal> amounts, DateTime dateTime, decimal amount, string period)
        {
            DateTime groupStart = GetGroupStart(dateTime, period);
            amounts.TryGetValue(groupStart, out decimal currentAmount);
            amounts[groupStart] = currentAmount + amount;
        }

        private static bool HasExpenseMoneyMovementInPeriod(Receipt receipt, DateTime periodStart, DateTime periodEnd)
        {
            return receipt.MoneyMovementLinks.Any(link =>
                link.MoneyMovement != null &&
                link.MoneyMovement.Type == MoneyMovementType.Expense &&
                link.MoneyMovement.OccurredAt >= periodStart &&
                link.MoneyMovement.OccurredAt < periodEnd);
        }

        private static List<DashboardIncomeExpensePointDto> BuildPoints(
            Dictionary<DateTime, decimal> incomeAmounts,
            Dictionary<DateTime, decimal> expenseAmounts,
            DateTime dateFrom,
            DateTime dateTo,
            string period)
        {
            List<DashboardIncomeExpensePointDto> points = new();

            foreach (DateTime groupStart in EnumeratePeriods(dateFrom, dateTo, period))
            {
                incomeAmounts.TryGetValue(groupStart, out decimal incomeAmount);
                expenseAmounts.TryGetValue(groupStart, out decimal expenseAmount);

                points.Add(new DashboardIncomeExpensePointDto
                {
                    PeriodStart = groupStart,
                    Label = FormatLabel(groupStart, period),
                    IncomeSum = incomeAmount,
                    ExpenseSum = expenseAmount
                });
            }

            return points;
        }

        private static IEnumerable<DateTime> EnumeratePeriods(DateTime dateFrom, DateTime dateTo, string period)
        {
            DateTime cursor = period == PERIOD_MONTH
                ? new DateTime(dateFrom.Year, dateFrom.Month, 1)
                : dateFrom.Date;

            DateTime end = period == PERIOD_MONTH
                ? new DateTime(dateTo.Year, dateTo.Month, 1)
                : dateTo.Date;

            while (cursor <= end)
            {
                yield return cursor;
                cursor = period == PERIOD_MONTH ? cursor.AddMonths(1) : cursor.AddDays(1);
            }
        }

        private static DateTime GetGroupStart(DateTime dateTime, string period)
        {
            return period == PERIOD_MONTH
                ? new DateTime(dateTime.Year, dateTime.Month, 1)
                : dateTime.Date;
        }

        private static string FormatLabel(DateTime dateTime, string period)
        {
            CultureInfo culture = CultureInfo.GetCultureInfo("ru-RU");
            return period == PERIOD_MONTH
                ? dateTime.ToString("MMMM yyyy", culture)
                : dateTime.ToString("dd.MM", culture);
        }

        private static string GetStoreName(Receipt receipt)
        {
            string? storeName = !string.IsNullOrWhiteSpace(receipt.Store?.AdaptiveName)
                ? receipt.Store.AdaptiveName
                : receipt.Store?.Name;

            if (string.IsNullOrWhiteSpace(storeName))
                storeName = receipt.User;

            return string.IsNullOrWhiteSpace(storeName)
                ? UNKNOWN_STORE_NAME
                : storeName.Trim();
        }

    }
}
