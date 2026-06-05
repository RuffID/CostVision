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

            List<DashboardAmountItem> incomeItems = BuildIncomeItems(moneyMovements);
            List<DashboardAmountItem> expenseItems = BuildExpenseItems(filteredReceipts, filteredMoneyMovements, expenseSource, periodStart, periodEnd);
            List<DashboardIncomeExpensePointDto> points = BuildPoints(incomeItems, expenseItems, periodStart, request.DateTo.Date, period);

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

        private static List<DashboardAmountItem> BuildIncomeItems(List<MoneyMovement> moneyMovements)
        {
            return moneyMovements
                .Where(movement => movement.Type == MoneyMovementType.Income)
                .Select(movement => new DashboardAmountItem
                {
                    DateTime = movement.OccurredAt,
                    Amount = movement.Amount
                })
                .ToList();
        }

        private static List<DashboardAmountItem> BuildExpenseItems(List<Receipt> receipts, List<MoneyMovement> moneyMovements, string expenseSource, DateTime periodStart, DateTime periodEnd)
        {
            if (expenseSource == EXPENSE_SOURCE_RECEIPTS)
                return BuildReceiptExpenseItems(receipts);

            List<MoneyMovement> expenseMovements = moneyMovements
                .Where(movement => movement.Type == MoneyMovementType.Expense)
                .ToList();

            if (expenseSource == EXPENSE_SOURCE_MONEY_MOVEMENTS)
                return BuildMoneyMovementExpenseItems(expenseMovements);

            List<DashboardAmountItem> items = BuildMoneyMovementExpenseItems(expenseMovements);
            items.AddRange(BuildReceiptExpenseItems(receipts.Where(receipt => !HasExpenseMoneyMovementInPeriod(receipt, periodStart, periodEnd)).ToList()));

            return items;
        }

        private static List<DashboardAmountItem> BuildReceiptExpenseItems(List<Receipt> receipts)
        {
            return receipts
                .Select(receipt => new DashboardAmountItem
                {
                    DateTime = receipt.DateTime,
                    Amount = receipt.TotalSum
                })
                .ToList();
        }

        private static List<DashboardAmountItem> BuildMoneyMovementExpenseItems(List<MoneyMovement> moneyMovements)
        {
            return moneyMovements
                .Select(movement => new DashboardAmountItem
                {
                    DateTime = movement.OccurredAt,
                    Amount = movement.Amount
                })
                .ToList();
        }

        private static bool HasExpenseMoneyMovementInPeriod(Receipt receipt, DateTime periodStart, DateTime periodEnd)
        {
            return receipt.MoneyMovementLinks.Any(link =>
                link.MoneyMovement != null &&
                link.MoneyMovement.Type == MoneyMovementType.Expense &&
                link.MoneyMovement.OccurredAt >= periodStart &&
                link.MoneyMovement.OccurredAt < periodEnd);
        }

        private static List<DashboardIncomeExpensePointDto> BuildPoints(List<DashboardAmountItem> incomeItems, List<DashboardAmountItem> expenseItems, DateTime dateFrom, DateTime dateTo, string period)
        {
            Dictionary<DateTime, List<DashboardAmountItem>> groupedIncomeItems = incomeItems
                .GroupBy(item => GetGroupStart(item.DateTime, period))
                .ToDictionary(group => group.Key, group => group.ToList());

            Dictionary<DateTime, List<DashboardAmountItem>> groupedExpenseItems = expenseItems
                .GroupBy(item => GetGroupStart(item.DateTime, period))
                .ToDictionary(group => group.Key, group => group.ToList());

            List<DashboardIncomeExpensePointDto> points = new();

            foreach (DateTime groupStart in EnumeratePeriods(dateFrom, dateTo, period))
            {
                groupedIncomeItems.TryGetValue(groupStart, out List<DashboardAmountItem>? groupIncomeItems);
                groupedExpenseItems.TryGetValue(groupStart, out List<DashboardAmountItem>? groupExpenseItems);

                points.Add(new DashboardIncomeExpensePointDto
                {
                    PeriodStart = groupStart,
                    Label = FormatLabel(groupStart, period),
                    IncomeSum = groupIncomeItems?.Sum(item => item.Amount) ?? 0m,
                    ExpenseSum = groupExpenseItems?.Sum(item => item.Amount) ?? 0m
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

        private class DashboardAmountItem
        {
            public DateTime DateTime { get; set; }
            public decimal Amount { get; set; }
        }
    }
}
