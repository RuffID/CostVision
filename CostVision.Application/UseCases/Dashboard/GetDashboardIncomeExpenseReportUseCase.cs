using CostVision.Application.Extensions;
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
            {
                return ServiceResult<DashboardIncomeExpenseReportDto>.Fail(ServiceErrorType.Validation, "Дата окончания периода не может быть меньше даты начала.");
            }

            if (!IsSupportedPeriod(request.Period))
            {
                return ServiceResult<DashboardIncomeExpenseReportDto>.Fail(ServiceErrorType.Validation, "Некорректная группировка отчёта.");
            }

            if (!IsSupportedExpenseSource(request.ExpenseSource))
            {
                return ServiceResult<DashboardIncomeExpenseReportDto>.Fail(ServiceErrorType.Validation, "Некорректный источник отчёта.");
            }

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
                            receipt.Accounts.Any(link => selectedAccountIds.Contains(link.AccountId) &&
                                link.Account != null &&
                                (link.Account.CreatedByUserId == currentUser.Id ||
                                 link.Account.Members.Any(member => member.UserId == currentUser.Id))) ||
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
                    .Include(movement => movement.ReceiptLinks)
                        .ThenInclude(link => link.Receipt)
                            .ThenInclude(receipt => receipt!.Accounts)
                                .ThenInclude(link => link.Account)
                                    .ThenInclude(account => account!.Members)
                    .AsSplitQuery(),
                ct: ct);

            List<Receipt> storeReceipts = new();
            if (expenseSource != EXPENSE_SOURCE_MONEY_MOVEMENTS)
            {
                storeReceipts.AddRange(receipts);
            }

            if (expenseSource != EXPENSE_SOURCE_RECEIPTS)
            {
                storeReceipts.AddRange(moneyMovements.SelectMany(movement => movement.ReceiptLinks)
                    .Where(link => FinancialDataAccess.CanReadReceipt(link.Receipt, currentUser.Id))
                    .Select(link => link.Receipt!));
            }

            List<string> stores = storeReceipts.Select(GetStoreName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(store => store)
                .ToList();

            HashSet<string> selectedStores = request.StoreNames
                .Where(store => !string.IsNullOrWhiteSpace(store))
                .Select(store => store.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            List<Receipt> filteredReceipts = receipts.Where(receipt =>
                ReceiptFinancialRules.GetDirection(receipt.OperationType) == MoneyMovementType.Income ||
                selectedStores.Count == 0 || selectedStores.Contains(GetStoreName(receipt))).ToList();
            List<MoneyMovement> filteredMoneyMovements = moneyMovements.Where(movement =>
                movement.Type == MoneyMovementType.Income || selectedStores.Count == 0 ||
                movement.ReceiptLinks.Any(link => FinancialDataAccess.CanReadReceipt(link.Receipt, currentUser.Id) &&
                    selectedStores.Contains(GetStoreName(link.Receipt!)))).ToList();

            List<ReportReceipt> documents = filteredReceipts.GroupBy(receipt => receipt.GetIdentity())
                .Select(group => new ReportReceipt(group.Key, group.SelectMany(receipt => receipt.MoneyMovementLinks)
                    .Select(link => link.MoneyMovementId).Distinct().ToList())).ToList();
            List<ReportMoneyMovement> payments = filteredMoneyMovements.DistinctBy(movement => movement.Id)
                .Select(movement => new ReportMoneyMovement(movement.Id, movement.OccurredAt, movement.Amount, movement.Type)).ToList();
            List<DashboardIncomeExpensePointDto> points = IncomeExpenseReportCalculator.Calculate(
                documents, payments, periodStart, request.DateTo.Date, period, expenseSource);

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
