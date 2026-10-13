using CostVision.Application.Abstractions.DataBase.Repositories;
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
    public class GetReceiptMoneyMovementCandidatesUseCase(IUnitOfWork unitOfWork) : IGetReceiptMoneyMovementCandidatesUseCase
    {
        private const decimal DEFAULT_TIME_WINDOW_HOURS = 1m;
        private const decimal MINUTES_PER_HOUR = 60m;
        private const decimal DEFAULT_AMOUNT_TOLERANCE = 1m;

        public async Task<ServiceResult<List<ReceiptMoneyMovementDto>>> ExecuteAsync(GetReceiptMoneyMovementCandidatesRequest request, Guid currentUserId, CancellationToken ct)
        {
            if (request.ReceiptId == Guid.Empty)
            {
                return ServiceResult<List<ReceiptMoneyMovementDto>>.Fail(ServiceErrorType.Validation, "Некорректный идентификатор чека.");
            }

            Expression<Func<Receipt, bool>> receiptPredicate = item => item.Id == request.ReceiptId;

            Receipt? receipt = await unitOfWork.Receipt.GetItemByPredicateAsync(
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
                return ServiceResult<List<ReceiptMoneyMovementDto>>.Fail(ServiceErrorType.NotFound, "Чек не найден или доступ к нему отсутствует.");
            }

            DateTime? dateFrom = request.DateFrom?.Date;
            DateTime? dateToExclusive = request.DateTo?.Date.AddDays(1);

            if (request.UseTimeWindow)
            {
                decimal timeWindowHours = request.TimeWindowHours.GetValueOrDefault(DEFAULT_TIME_WINDOW_HOURS);
                if (timeWindowHours < 0)
                {
                    return ServiceResult<List<ReceiptMoneyMovementDto>>.Fail(ServiceErrorType.Validation, "Допуск по времени не может быть отрицательным.");
                }

                double timeWindowMinutes = (double)(timeWindowHours * MINUTES_PER_HOUR);
                dateFrom = receipt.DateTime.AddMinutes(-timeWindowMinutes);
                dateToExclusive = receipt.DateTime.AddMinutes(timeWindowMinutes);
            }

            if (dateFrom.HasValue && dateToExclusive.HasValue && dateToExclusive.Value < dateFrom.Value)
            {
                return ServiceResult<List<ReceiptMoneyMovementDto>>.Fail(ServiceErrorType.Validation, "Дата окончания периода не может быть меньше даты начала.");
            }

            decimal amountTolerance = request.AmountTolerance.GetValueOrDefault(DEFAULT_AMOUNT_TOLERANCE);
            if (amountTolerance < 0)
            {
                return ServiceResult<List<ReceiptMoneyMovementDto>>.Fail(ServiceErrorType.Validation, "Допуск по сумме не может быть отрицательным.");
            }

            Expression<Func<MoneyMovement, bool>> movementPredicate = ReceiptMoneyMovementCandidateRules.BuildPredicate(
                receipt, currentUserId, dateFrom, dateToExclusive, request.UseAmountFilter,
                amountTolerance, request.ExcludeLinkedMoneyMovements);

            List<MoneyMovement> movements = await unitOfWork.MoneyMovement.GetItemsByPredicateAsync(
                movementPredicate,
                asNoTracking: true,
                include: query => query
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

            return ServiceResult<List<ReceiptMoneyMovementDto>>.Ok(result);
        }
    }
}
