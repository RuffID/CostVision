using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.Abstractions.DataBase.Repositories.MoneyMovements;
using CostVision.Application.Models.Dtos;
using CostVision.Application.UseCases.Receipts.Receipts;
using CostVision.Application.Models.Dtos.MoneyMovements;
using CostVision.Application.Models.Requests.MoneyMovements;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.MoneyMovements.Helpers;
using CostVision.Domain.Models.MoneyMovements;
using CostVision.Domain.Models.Receipts;
using Microsoft.EntityFrameworkCore;
using CostVision.Application.Extensions;
using System.Linq.Expressions;

namespace CostVision.Application.UseCases.MoneyMovements
{
    public class GetMoneyMovementReceiptCandidatesUseCase(IMoneyMovementRepository moneyMovementRepository, IReceiptRepository receiptRepository) : IGetMoneyMovementReceiptCandidatesUseCase
    {
        private const decimal DEFAULT_TIME_WINDOW_HOURS = 1m;
        private const decimal MINUTES_PER_HOUR = 60m;
        private const decimal DEFAULT_AMOUNT_TOLERANCE = 1m;

        public async Task<ServiceResult<PagedListDto<MoneyMovementReceiptDto>>> ExecuteAsync(GetMoneyMovementReceiptCandidatesRequest request, Guid currentUserId, CancellationToken ct)
        {
            if (request.MoneyMovementId == Guid.Empty)
            {
                return ServiceResult<PagedListDto<MoneyMovementReceiptDto>>.Fail(ServiceErrorType.Validation, "Некорректный идентификатор операции.");
            }

            Expression<Func<MoneyMovement, bool>> movementPredicate = item => item.Id == request.MoneyMovementId;

            MoneyMovement? movement = await moneyMovementRepository.GetItemByPredicateAsync(
                movementPredicate.WithAccountReadAccess(item => item.Account, currentUserId),
                asNoTracking: true,
                include: query => query
                    .Include(item => item.Account)
                        .ThenInclude(account => account!.Members),
                ct: ct);

            if (movement == null)
            {
                return ServiceResult<PagedListDto<MoneyMovementReceiptDto>>.Fail(ServiceErrorType.NotFound, "Операция не найдена или доступ к ней отсутствует.");
            }

            DateTime? dateFrom = request.DateFrom?.Date;
            DateTime? dateToExclusive = request.DateTo?.Date.AddDays(1);

            if (request.UseTimeWindow)
            {
                decimal timeWindowHours = request.TimeWindowHours.GetValueOrDefault(DEFAULT_TIME_WINDOW_HOURS);
                if (timeWindowHours < 0)
                {
                    return ServiceResult<PagedListDto<MoneyMovementReceiptDto>>.Fail(ServiceErrorType.Validation, "Допуск по времени не может быть отрицательным.");
                }

                double timeWindowMinutes = (double)(timeWindowHours * MINUTES_PER_HOUR);
                dateFrom = movement.OccurredAt.AddMinutes(-timeWindowMinutes);
                dateToExclusive = movement.OccurredAt.AddMinutes(timeWindowMinutes);
            }

            if (dateFrom.HasValue && dateToExclusive.HasValue && dateToExclusive.Value < dateFrom.Value)
            {
                return ServiceResult<PagedListDto<MoneyMovementReceiptDto>>.Fail(ServiceErrorType.Validation, "Дата окончания периода не может быть меньше даты начала.");
            }

            decimal amountTolerance = request.AmountTolerance.GetValueOrDefault(DEFAULT_AMOUNT_TOLERANCE);
            if (amountTolerance < 0)
            {
                return ServiceResult<PagedListDto<MoneyMovementReceiptDto>>.Fail(ServiceErrorType.Validation, "Допуск по сумме не может быть отрицательным.");
            }

            Expression<Func<Receipt, bool>> receiptPredicate = receipt => (!dateFrom.HasValue || receipt.DateTime >= dateFrom.Value) &&
                           (!dateToExclusive.HasValue || receipt.DateTime < dateToExclusive.Value) &&
                           (!request.UseAmountFilter || Math.Abs(receipt.TotalSum - movement.Amount) <= amountTolerance) &&
                           (!request.ExcludeLinkedReceipts || !receipt.MoneyMovementLinks.Any()) &&
                           (receipt.Accounts.Any(link => link.AccountId == movement.AccountId) ||
                            (!receipt.Accounts.Any() && receipt.CreatedByUserId == currentUserId));

            receiptPredicate = receiptPredicate.WithReceiptReadAccess(item => item, currentUserId);
            List<int> counts = await receiptRepository.GetProjectedItemsAsync(receiptPredicate,
                query => ReceiptReadQueries.Documents(query).GroupBy(item => 1).Select(group => group.Count()), ct);
            int count = counts.SingleOrDefault();
            int pageSize = Math.Clamp(request.PageSize, 1, 100);
            int pages = Math.Max(1, (int)Math.Ceiling(count / (double)pageSize));
            int page = Math.Clamp(request.Page, 1, pages);
            List<Guid> ids = await receiptRepository.GetProjectedItemsAsync(receiptPredicate,
                query => ReadQueryOrdering.ByDistance(ReceiptReadQueries.Documents(query), item => item.DateTime,
                    movement.OccurredAt, item => item.TotalSum, movement.Amount, item => item.Id)
                    .Skip((page - 1) * pageSize).Take(pageSize).Select(item => item.Id), ct);
            List<Receipt> receipts = [];
            if (ids.Count > 0)
            {
                receipts = await receiptRepository.GetProjectedItemsAsync(receiptPredicate,
                    query => ReceiptReadQueries.WithReportGraph(ReceiptReadQueries.CopiesForPage(query, ids)), ct);
            }

            Dictionary<ReceiptIdentity, int> positions = receipts.Where(copy => ids.Contains(copy.Id))
                .ToDictionary(copy => copy.GetIdentity(), copy => ids.IndexOf(copy.Id));

            List<MoneyMovementReceiptDto> result = receipts
                .OrderBy(receipt => positions[receipt.GetIdentity()])
                .ThenBy(receipt => receipt.Id.ToString(), StringComparer.Ordinal)
                .Select(receipt => receipt.MapReceiptLinkDto(currentUserId))
                .ToList();

            return ServiceResult<PagedListDto<MoneyMovementReceiptDto>>.Ok(PagedListDto<MoneyMovementReceiptDto>.Create(result, count, page, pageSize));
        }
    }
}
