using System.Linq.Expressions;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.Abstractions.DataBase.Repositories.MoneyMovements;
using CostVision.Application.Extensions;
using CostVision.Application.Models.Dtos;
using CostVision.Application.Models.Dtos.MoneyMovements;
using CostVision.Application.Models.Responses.Results;
using CostVision.Domain.Models.Enums.MoneyMovements;
using CostVision.Domain.Models.MoneyMovements;
using CostVision.Domain.Models.Receipts;

namespace CostVision.Application.UseCases.MoneyMovements;

public class GetMoneyMovementListUseCase(IMoneyMovementRepository moneyMovementRepository, IReceiptRepository receiptRepository) : IGetMoneyMovementListUseCase
{
    public async Task<ServiceResult<MoneyMovementListDto>> ExecuteAsync(Guid currentUserId, DateTime dateFrom,
        DateTime dateTo, Guid? accountId, CancellationToken ct, int page = 1, int pageSize = 20,
        string? search = null, string? receiptFilter = null)
    {
        ServiceResult dateRange = RequestDateValidation.ValidateCalendarRange(dateFrom, dateTo);
        if (!dateRange.Success)
        {
            return dateRange.PropagateFailure<MoneyMovementListDto>();
        }


        DateTime start = dateFrom.Date;
        DateTime end = dateTo.Date.AddDays(1);
        string text = search?.Trim().ToLowerInvariant() ?? "";
        Expression<Func<Receipt, bool>> visibleReceipt = ((Expression<Func<Receipt, bool>>)(receipt => true)).WithReceiptReadAccess(receipt => receipt, currentUserId);
        Expression<Func<MoneyMovement, bool>> predicate = movement =>
            movement.OccurredAt >= start && movement.OccurredAt < end &&
            (!accountId.HasValue || movement.AccountId == accountId.Value) &&
            (text == "" || (movement.Comment != null && movement.Comment.ToLower().Contains(text)) ||
                (movement.ImportComment != null && movement.ImportComment.ToLower().Contains(text))) &&
            (receiptFilter != "withReceipts" || movement.ReceiptLinks.Select(link => link.Receipt!).AsQueryable().Any(visibleReceipt)) &&
            (receiptFilter != "withoutReceipts" || !movement.ReceiptLinks.Select(link => link.Receipt!).AsQueryable().Any(visibleReceipt)) &&
            (receiptFilter != "amountMismatch" ||
                (movement.ReceiptLinks.Select(link => link.Receipt!).AsQueryable().Any(visibleReceipt) &&
                 Math.Abs(movement.Amount - movement.ReceiptLinks.Select(link => link.Receipt!).AsQueryable()
                     .Where(visibleReceipt).Sum(receipt => receipt.TotalSum)) >= 0.01m));
        predicate = predicate.WithAccountReadAccess(movement => movement.Account, currentUserId);
        List<ReadQueryTotals> summaries = await moneyMovementRepository.GetProjectedItemsAsync(predicate,
            query => query.GroupBy(movement => 1).Select(group => new ReadQueryTotals
            {
                Count = group.Count(),
                Income = group.Sum(movement => movement.Type == MoneyMovementType.Income ? movement.Amount : 0m),
                Expense = group.Sum(movement => movement.Type == MoneyMovementType.Expense ? movement.Amount : 0m)
            }), ct);
        ReadQueryTotals totals = summaries.SingleOrDefault() ?? new ReadQueryTotals();
        pageSize = Math.Clamp(pageSize, 1, 100);
        int pages = Math.Max(1, (int)Math.Ceiling(totals.Count / (double)pageSize));
        page = Math.Clamp(page, 1, pages);
        List<MoneyMovementDto> items = await moneyMovementRepository.GetProjectedItemsAsync(predicate,
            query => query.OrderByDescending(movement => movement.OccurredAt).ThenBy(movement => movement.Id)
                .Skip((page - 1) * pageSize).Take(pageSize).Select(movement => new MoneyMovementDto
                {
                    Id = movement.Id, AccountId = movement.AccountId == Guid.Empty ? null : movement.AccountId,
                    AccountName = movement.Account != null ? movement.Account.Name : "",
                    AccountColorHex = movement.Account != null ? movement.Account.ColorHex : "",
                    Amount = movement.Amount, Type = movement.Type, OccurredAt = movement.OccurredAt,
                    Comment = movement.Comment, ImportComment = movement.ImportComment,
                    PerformedByUserId = movement.PerformedByUserId,
                    PerformedByUserName = movement.PerformedByUser != null ? movement.PerformedByUser.Name : "",
                    Source = movement.Source,
                    LinkedReceiptCount = movement.ReceiptLinks.Select(link => link.Receipt!).AsQueryable().Count(visibleReceipt),
                    LinkedReceiptsTotalSum = movement.ReceiptLinks.Select(link => link.Receipt!).AsQueryable()
                        .Where(visibleReceipt).Sum(receipt => receipt.TotalSum)
                }), ct);
        foreach (MoneyMovementDto movement in items)
        {
            DateTime day = movement.OccurredAt.Date;
            DateTime nextDay = day.AddDays(1);
            Expression<Func<Receipt, bool>> candidates = receipt => receipt.DateTime >= day && receipt.DateTime < nextDay &&
                receipt.TotalSum == movement.Amount && !receipt.MoneyMovementLinks.Any() &&
                (movement.AccountId == Guid.Empty ? !receipt.Accounts.Any() : receipt.Accounts.Any(link => link.AccountId == movement.AccountId));
            candidates = candidates.WithReceiptReadAccess(receipt => receipt, currentUserId);
            List<int> counts = await receiptRepository.GetProjectedItemsAsync(candidates,
                query => query.GroupBy(receipt => 1).Select(group => group.Count()), ct);
            movement.AvailableReceiptCount = counts.SingleOrDefault();
        }

        return ServiceResult<MoneyMovementListDto>.Ok(new MoneyMovementListDto
        {
            Items = items, Page = page, PageSize = pageSize, TotalCount = totals.Count, TotalPages = pages,
            HasPreviousPage = page > 1, HasNextPage = page < pages,
            IncomeSum = totals.Income, ExpenseSum = totals.Expense
        });
    }
}
