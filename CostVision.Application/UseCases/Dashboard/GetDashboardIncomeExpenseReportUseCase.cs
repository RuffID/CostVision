using CostVision.Application.Extensions;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.MoneyMovements;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.Models.Dtos.Dashboard;
using CostVision.Application.Models.Requests.Dashboard;
using CostVision.Application.Models.Responses.Results;
using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.Enums.MoneyMovements;
using CostVision.Domain.Models.MoneyMovements;
using CostVision.Domain.Models.Receipts;
using System.Linq.Expressions;
using CostVision.Domain.Models.Enums.Receipts;

namespace CostVision.Application.UseCases.Dashboard
{
    public class GetDashboardIncomeExpenseReportUseCase(IReceiptRepository receiptRepository, IMoneyMovementRepository moneyMovementRepository) : IGetDashboardIncomeExpenseReportUseCase
    {
        private const string PERIOD_DAY = "day";
        private const string PERIOD_MONTH = "month";
        private const string EXPENSE_SOURCE_RECEIPTS = "receipts";
        private const string EXPENSE_SOURCE_MONEY_MOVEMENTS = "moneyMovements";
        private const string EXPENSE_SOURCE_RECEIPTS_AND_MONEY_MOVEMENTS = "receiptsAndMoneyMovements";
        private const string UNKNOWN_STORE_NAME = "Без магазина";

        public async Task<ServiceResult<DashboardIncomeExpenseReportDto>> ExecuteAsync(User currentUser, DashboardIncomeExpenseReportRequest request, CancellationToken ct)
        {
            ServiceResult requestShape = RequestShapeValidation.Validate(request);
            if (!requestShape.Success)
            {
                return requestShape.PropagateFailure<DashboardIncomeExpenseReportDto>();
            }

            ServiceResult dateRange = RequestDateValidation.ValidateCalendarRange(request.DateFrom, request.DateTo);
            if (!dateRange.Success)
            {
                return dateRange.PropagateFailure<DashboardIncomeExpenseReportDto>();
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

            Expression<Func<Receipt, bool>> receiptPredicate = receipt => receipt.DateTime >= periodStart &&
                receipt.DateTime < periodEnd && (!hasAccountFilter ||
                    receipt.Accounts.Any(link => selectedAccountIds.Contains(link.AccountId) && link.Account != null &&
                        (link.Account.CreatedByUserId == currentUser.Id || link.Account.Members.Any(member => member.UserId == currentUser.Id))) ||
                    (request.IncludeWithoutAccount && !receipt.Accounts.Any()));
            receiptPredicate = receiptPredicate.WithReceiptReadAccess(receipt => receipt, currentUser.Id);
            Expression<Func<MoneyMovement, bool>> movementPredicate = movement => movement.OccurredAt >= periodStart &&
                movement.OccurredAt < periodEnd && (!hasAccountFilter || selectedAccountIds.Contains(movement.AccountId));
            movementPredicate = movementPredicate.WithAccountReadAccess(movement => movement.Account, currentUser.Id);
            Expression<Func<Receipt, bool>> visibleReceipt =
                ((Expression<Func<Receipt, bool>>)(receipt => true)).WithReceiptReadAccess(receipt => receipt, currentUser.Id);
            Expression<Func<Receipt, string>> storeName = receipt =>
                (receipt.Store != null && receipt.Store.AdaptiveName != null && receipt.Store.AdaptiveName.Trim() != "" ? receipt.Store.AdaptiveName :
                 receipt.Store != null && receipt.Store.Name.Trim() != "" ? receipt.Store.Name :
                 receipt.User != null && receipt.User.Trim() != "" ? receipt.User : UNKNOWN_STORE_NAME).Trim();
            List<string> stores = [];
            if (expenseSource != EXPENSE_SOURCE_MONEY_MOVEMENTS)
            {
                stores.AddRange(await receiptRepository.GetProjectedItemsAsync(receiptPredicate,
                    query => query.Select(storeName).Distinct(), ct));
            }

            if (expenseSource != EXPENSE_SOURCE_RECEIPTS)
            {
                stores.AddRange(await moneyMovementRepository.GetProjectedItemsAsync(movementPredicate,
                    query => query.SelectMany(movement => movement.ReceiptLinks.Select(link => link.Receipt!))
                        .Where(visibleReceipt).Select(storeName).Distinct(), ct));
            }

            stores = stores.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name).ToList();
            List<string> selectedStores = request.StoreNames.Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim().ToLowerInvariant()).Distinct().ToList();
            Expression<Func<Receipt, bool>> selectedStore = receipt =>
                selectedStores.Contains((receipt.Store != null && receipt.Store.AdaptiveName != null && receipt.Store.AdaptiveName.Trim() != "" ? receipt.Store.AdaptiveName :
                    receipt.Store != null && receipt.Store.Name.Trim() != "" ? receipt.Store.Name :
                    receipt.User != null && receipt.User.Trim() != "" ? receipt.User : UNKNOWN_STORE_NAME).Trim().ToLower());
            List<DashboardReceiptReadDto> receiptRows = [];
            if (expenseSource != EXPENSE_SOURCE_MONEY_MOVEMENTS)
            {
                receiptRows = await receiptRepository.GetProjectedItemsAsync(receiptPredicate,
                    query => query.Where(receipt => receipt.OperationType == ReceiptOperationType.Expense ||
                        receipt.OperationType == ReceiptOperationType.RefundIncome || selectedStores.Count == 0 ||
                        selectedStores.Contains((receipt.Store != null && receipt.Store.AdaptiveName != null && receipt.Store.AdaptiveName.Trim() != "" ? receipt.Store.AdaptiveName :
                            receipt.Store != null && receipt.Store.Name.Trim() != "" ? receipt.Store.Name :
                            receipt.User != null && receipt.User.Trim() != "" ? receipt.User : UNKNOWN_STORE_NAME).Trim().ToLower()))
                        .Select(receipt => new DashboardReceiptReadDto
                        {
                            Identity = new ReceiptIdentity(receipt.FiscalDriveNumber, receipt.FiscalDocumentNumber, receipt.FiscalSign,
                                receipt.DateTime, receipt.TotalSum, receipt.OperationType),
                            MovementIds = receipt.MoneyMovementLinks.Select(link => link.MoneyMovementId).ToList()
                        }), ct);
            }

            List<DashboardPaymentReadDto> paymentRows = [];
            if (expenseSource != EXPENSE_SOURCE_RECEIPTS)
            {
                paymentRows = await moneyMovementRepository.GetProjectedItemsAsync(movementPredicate,
                    query => query.Where(movement => movement.Type == MoneyMovementType.Income || selectedStores.Count == 0 ||
                        movement.ReceiptLinks.Select(link => link.Receipt!).AsQueryable().Where(visibleReceipt).Any(selectedStore))
                        .Select(movement => new DashboardPaymentReadDto
                        {
                            Id = movement.Id, Date = movement.OccurredAt, Amount = movement.Amount, Type = movement.Type
                        }), ct);
            }

            List<ReportReceipt> documents = receiptRows.GroupBy(receipt => receipt.Identity)
                .Select(group => new ReportReceipt(group.Key, group.SelectMany(receipt => receipt.MovementIds).Distinct().ToList())).ToList();
            List<ReportMoneyMovement> payments = paymentRows.DistinctBy(movement => movement.Id)
                .Select(movement => new ReportMoneyMovement(movement.Id, movement.Date, movement.Amount, movement.Type)).ToList();
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

    }
}
