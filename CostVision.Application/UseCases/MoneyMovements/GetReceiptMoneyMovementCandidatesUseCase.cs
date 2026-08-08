using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Models.Dtos.MoneyMovements;
using CostVision.Application.Models.Requests.MoneyMovements;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.MoneyMovements.Helpers;
using CostVision.Domain.Models.MoneyMovements;
using CostVision.Domain.Models.Receipts;
using Microsoft.EntityFrameworkCore;

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
                return ServiceResult<List<ReceiptMoneyMovementDto>>.Fail(ServiceErrorType.Validation, "Некорректный идентификатор чека.");

            Receipt? receipt = await unitOfWork.Receipt.GetItemByPredicateAsync(
                item => item.Id == request.ReceiptId &&
                        (item.CreatedByUserId == currentUserId ||
                         item.Accounts.Any(link => link.Account!.CreatedByUserId == currentUserId) ||
                         item.Accounts.Any(link => link.Account!.Members.Any(member => member.UserId == currentUserId))),
                asNoTracking: true,
                include: query => query
                    .Include(item => item.Accounts)
                        .ThenInclude(link => link.Account)
                            .ThenInclude(account => account!.Members)
                    .Include(item => item.MoneyMovementLinks)
                    .AsSplitQuery(),
                ct: ct);

            if (receipt == null)
                return ServiceResult<List<ReceiptMoneyMovementDto>>.Fail(ServiceErrorType.NotFound, "Чек не найден или доступ к нему отсутствует.");

            DateTime? dateFrom = request.DateFrom?.Date;
            DateTime? dateToExclusive = request.DateTo?.Date.AddDays(1);

            if (request.UseTimeWindow)
            {
                decimal timeWindowHours = request.TimeWindowHours.GetValueOrDefault(DEFAULT_TIME_WINDOW_HOURS);
                if (timeWindowHours < 0)
                    return ServiceResult<List<ReceiptMoneyMovementDto>>.Fail(ServiceErrorType.Validation, "Допуск по времени не может быть отрицательным.");

                double timeWindowMinutes = (double)(timeWindowHours * MINUTES_PER_HOUR);
                dateFrom = receipt.DateTime.AddMinutes(-timeWindowMinutes);
                dateToExclusive = receipt.DateTime.AddMinutes(timeWindowMinutes);
            }

            if (dateFrom.HasValue && dateToExclusive.HasValue && dateToExclusive.Value < dateFrom.Value)
                return ServiceResult<List<ReceiptMoneyMovementDto>>.Fail(ServiceErrorType.Validation, "Дата окончания периода не может быть меньше даты начала.");

            decimal amountTolerance = request.AmountTolerance.GetValueOrDefault(DEFAULT_AMOUNT_TOLERANCE);
            if (amountTolerance < 0)
                return ServiceResult<List<ReceiptMoneyMovementDto>>.Fail(ServiceErrorType.Validation, "Допуск по сумме не может быть отрицательным.");

            List<Guid> receiptAccountIds = receipt.Accounts.Select(link => link.AccountId).ToList();

            List<MoneyMovement> movements = await unitOfWork.MoneyMovement.GetItemsByPredicateAsync(
                movement => (!dateFrom.HasValue || movement.OccurredAt >= dateFrom.Value) &&
                            (!dateToExclusive.HasValue || movement.OccurredAt < dateToExclusive.Value) &&
                            (!request.UseAmountFilter || Math.Abs(movement.Amount - receipt.TotalSum) <= amountTolerance) &&
                            (!request.ExcludeLinkedMoneyMovements || !movement.ReceiptLinks.Any()) &&
                            (receiptAccountIds.Contains(movement.AccountId) ||
                             (receiptAccountIds.Count == 0 && movement.CreatedByUserId == currentUserId)) &&
                            (movement.Account!.CreatedByUserId == currentUserId ||
                             movement.Account.Members.Any(member => member.UserId == currentUserId)),
                asNoTracking: true,
                include: query => query
                    .Include(movement => movement.Account)
                        .ThenInclude(account => account!.Members)
                    .Include(movement => movement.ReceiptLinks)
                    .AsSplitQuery(),
                ct: ct);

            List<ReceiptMoneyMovementDto> result = movements
                .OrderBy(movement => Math.Abs((movement.OccurredAt - receipt.DateTime).Ticks))
                .ThenBy(movement => Math.Abs(movement.Amount - receipt.TotalSum))
                .Select(movement => movement.MapReceiptMoneyMovementDto())
                .ToList();

            return ServiceResult<List<ReceiptMoneyMovementDto>>.Ok(result);
        }
    }
}
