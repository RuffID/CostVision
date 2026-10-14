using CostVision.Application.Models.Dtos;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.MoneyMovements;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
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
    public class GetReceiptMoneyMovementCandidatesUseCase(IReceiptRepository receiptRepository, IMoneyMovementRepository moneyMovementRepository) : IGetReceiptMoneyMovementCandidatesUseCase
    {
        private const decimal DEFAULT_TIME_WINDOW_HOURS = 1m;
        private const decimal DEFAULT_AMOUNT_TOLERANCE = 1m;

        public async Task<ServiceResult<PagedListDto<ReceiptMoneyMovementDto>>> ExecuteAsync(GetReceiptMoneyMovementCandidatesRequest request, Guid currentUserId, CancellationToken ct)
        {
            ServiceResult requestShape = RequestShapeValidation.Validate(request);
            if (!requestShape.Success)
            {
                return requestShape.PropagateFailure<PagedListDto<ReceiptMoneyMovementDto>>();
            }

            ServiceResult dateRange = ServiceResult.Ok();
            if (!request.UseTimeWindow)
            {
                dateRange = RequestDateValidation.ValidateCalendarRange(request.DateFrom, request.DateTo);
            }

            if (!dateRange.Success)
            {
                return dateRange.PropagateFailure<PagedListDto<ReceiptMoneyMovementDto>>();
            }

            if (request.ReceiptId == Guid.Empty)
            {
                return ServiceResult<PagedListDto<ReceiptMoneyMovementDto>>.Fail(ServiceErrorType.Validation, "Некорректный идентификатор чека.");
            }

            Expression<Func<Receipt, bool>> receiptPredicate = item => item.Id == request.ReceiptId;

            Receipt? receipt = await receiptRepository.GetItemByPredicateAsync(
                receiptPredicate.WithReceiptReadAccess(item => item, currentUserId),
                asNoTracking: true,
                include: query => query
                    .Include(item => item.Accounts)
                        .ThenInclude(link => link.Account)
                            .ThenInclude(account => account!.Members)
                    .Include(item => item.MoneyMovementLinks)
                    .AsSplitQuery(),
                ct: ct);

            if (receipt == null)
            {
                return ServiceResult<PagedListDto<ReceiptMoneyMovementDto>>.Fail(ServiceErrorType.NotFound, "Чек не найден или доступ к нему отсутствует.");
            }

            DateTime? dateFrom = null;
            DateTime? dateToExclusive = null;
            if (request.UseTimeWindow)
            {
                decimal timeWindowHours = request.TimeWindowHours.GetValueOrDefault(DEFAULT_TIME_WINDOW_HOURS);
                ServiceResult window = RequestDateValidation.TryTimeWindow(receipt.DateTime, timeWindowHours, out DateTime windowFrom, out DateTime windowTo);
                if (!window.Success)
                {
                    return window.PropagateFailure<PagedListDto<ReceiptMoneyMovementDto>>();
                }

                dateFrom = windowFrom;
                dateToExclusive = windowTo;
            }
            else
            {
                dateFrom = request.DateFrom?.Date;
                dateToExclusive = request.DateTo?.Date.AddDays(1);
            }

            decimal amountTolerance = request.AmountTolerance.GetValueOrDefault(DEFAULT_AMOUNT_TOLERANCE);
            if (amountTolerance < 0)
            {
                return ServiceResult<PagedListDto<ReceiptMoneyMovementDto>>.Fail(ServiceErrorType.Validation, "Допуск по сумме не может быть отрицательным.");
            }

            Expression<Func<MoneyMovement, bool>> movementPredicate = ReceiptMoneyMovementCandidateRules.BuildPredicate(
                receipt, currentUserId, dateFrom, dateToExclusive, request.UseAmountFilter,
                amountTolerance, request.ExcludeLinkedMoneyMovements);


            List<int> counts = await moneyMovementRepository.GetProjectedItemsAsync(movementPredicate,
                query => query.GroupBy(item => 1).Select(group => group.Count()), ct);
            int count = counts.SingleOrDefault();
            int pageSize = Math.Clamp(request.PageSize, 1, 100);
            int pages = Math.Max(1, (int)Math.Ceiling(count / (double)pageSize));
            int page = Math.Clamp(request.Page, 1, pages);

            List<MoneyMovement> movements = await moneyMovementRepository.GetItemsByPredicateAsync(
                movementPredicate,
                skip: (page - 1) * pageSize,
                take: pageSize,
                asNoTracking: true,
                include: query => ReadQueryOrdering.ByDistance(query, movement => movement.OccurredAt, receipt.DateTime,
                    movement => movement.Amount, receipt.TotalSum, movement => movement.Id)
                    .Include(movement => movement.Account)
                        .ThenInclude(account => account!.Members)
                    .Include(movement => movement.ReceiptLinks)
                        .ThenInclude(link => link.Receipt)
                            .ThenInclude(receipt => receipt!.Accounts)
                                .ThenInclude(link => link.Account)
                                    .ThenInclude(account => account!.Members)
                    .AsSplitQuery(),
                ct: ct);

            List<ReceiptMoneyMovementDto> result = movements
                .OrderBy(movement => Math.Abs((movement.OccurredAt - receipt.DateTime).Ticks))
                .ThenBy(movement => Math.Abs(movement.Amount - receipt.TotalSum))
                .Select(movement => movement.MapReceiptMoneyMovementDto(currentUserId))
                .ToList();

            return ServiceResult<PagedListDto<ReceiptMoneyMovementDto>>.Ok(PagedListDto<ReceiptMoneyMovementDto>.Create(result, count, page, pageSize));
        }
    }
}
