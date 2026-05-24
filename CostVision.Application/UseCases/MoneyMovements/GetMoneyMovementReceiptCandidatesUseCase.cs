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
    public class GetMoneyMovementReceiptCandidatesUseCase(IUnitOfWork unitOfWork) : IGetMoneyMovementReceiptCandidatesUseCase
    {
        private const int DEFAULT_TIME_WINDOW_MINUTES = 60;
        private const decimal DEFAULT_AMOUNT_TOLERANCE = 1m;

        public async Task<ServiceResult<List<MoneyMovementReceiptDto>>> ExecuteAsync(GetMoneyMovementReceiptCandidatesRequest request, Guid currentUserId, CancellationToken ct)
        {
            if (request.MoneyMovementId == Guid.Empty)
                return ServiceResult<List<MoneyMovementReceiptDto>>.Fail(400, "Некорректный идентификатор операции.");

            MoneyMovement? movement = await unitOfWork.MoneyMovement.GetItemByPredicateAsync(
                item => item.Id == request.MoneyMovementId &&
                        (item.Account!.CreatedByUserId == currentUserId ||
                         item.Account.Members.Any(member => member.UserId == currentUserId)),
                asNoTracking: true,
                include: query => query
                    .Include(item => item.Account)
                        .ThenInclude(account => account!.Members),
                ct: ct);

            if (movement == null)
                return ServiceResult<List<MoneyMovementReceiptDto>>.Fail(404, "Операция не найдена или доступ к ней отсутствует.");

            DateTime? dateFrom = request.DateFrom?.Date;
            DateTime? dateToExclusive = request.DateTo?.Date.AddDays(1);

            if (request.UseTimeWindow)
            {
                dateFrom = movement.OccurredAt.AddMinutes(-DEFAULT_TIME_WINDOW_MINUTES);
                dateToExclusive = movement.OccurredAt.AddMinutes(DEFAULT_TIME_WINDOW_MINUTES);
            }

            if (dateFrom.HasValue && dateToExclusive.HasValue && dateToExclusive.Value < dateFrom.Value)
                return ServiceResult<List<MoneyMovementReceiptDto>>.Fail(400, "Дата окончания периода не может быть меньше даты начала.");

            decimal amountTolerance = request.AmountTolerance.GetValueOrDefault(DEFAULT_AMOUNT_TOLERANCE);
            if (amountTolerance < 0)
                return ServiceResult<List<MoneyMovementReceiptDto>>.Fail(400, "Допуск по сумме не может быть отрицательным.");

            List<Receipt> receipts = await unitOfWork.Receipt.GetItemsByPredicateAsync(
                receipt => (!dateFrom.HasValue || receipt.DateTime >= dateFrom.Value) &&
                           (!dateToExclusive.HasValue || receipt.DateTime < dateToExclusive.Value) &&
                           (!request.UseAmountFilter || Math.Abs(receipt.TotalSum - movement.Amount) <= amountTolerance) &&
                           (!request.ExcludeLinkedReceipts || !receipt.MoneyMovementLinks.Any()) &&
                           (receipt.CreatedByUserId == currentUserId ||
                            receipt.Accounts.Any(link => link.Account!.CreatedByUserId == currentUserId) ||
                            receipt.Accounts.Any(link => link.Account!.Members.Any(member => member.UserId == currentUserId))) &&
                           (receipt.Accounts.Any(link => link.AccountId == movement.AccountId) ||
                            (!receipt.Accounts.Any() && receipt.CreatedByUserId == currentUserId)),
                asNoTracking: true,
                include: query => query
                    .Include(receipt => receipt.Accounts)
                        .ThenInclude(link => link.Account)
                            .ThenInclude(account => account!.Members)
                    .Include(receipt => receipt.MoneyMovementLinks)
                    .AsSplitQuery(),
                ct: ct);

            List<MoneyMovementReceiptDto> result = receipts
                .OrderBy(receipt => Math.Abs((receipt.DateTime - movement.OccurredAt).Ticks))
                .ThenBy(receipt => Math.Abs(receipt.TotalSum - movement.Amount))
                .Select(receipt => receipt.MapReceiptLinkDto())
                .ToList();

            return ServiceResult<List<MoneyMovementReceiptDto>>.Ok(result);
        }
    }
}
