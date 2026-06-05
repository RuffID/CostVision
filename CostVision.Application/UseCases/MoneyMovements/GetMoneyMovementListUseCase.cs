using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Models.Dtos.MoneyMovements;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.MoneyMovements.Helpers;
using CostVision.Domain.Models.MoneyMovements;
using CostVision.Domain.Models.Receipts;
using Microsoft.EntityFrameworkCore;

namespace CostVision.Application.UseCases.MoneyMovements
{
    public class GetMoneyMovementListUseCase(IUnitOfWork unitOfWork) : IGetMoneyMovementListUseCase
    {
        public async Task<ServiceResult<List<MoneyMovementDto>>> ExecuteAsync(Guid currentUserId, DateTime dateFrom, DateTime dateTo, Guid? accountId, CancellationToken ct)
        {
            if (dateTo < dateFrom)
                return ServiceResult<List<MoneyMovementDto>>.Fail(400, "Дата окончания периода не может быть меньше даты начала.");

            DateTime periodEnd = dateTo.Date.AddDays(1);

            List<MoneyMovement> movements = await unitOfWork.MoneyMovement.GetItemsByPredicateAsync(
                movement => movement.OccurredAt >= dateFrom.Date &&
                            movement.OccurredAt < periodEnd &&
                            (!accountId.HasValue || movement.AccountId == accountId.Value) &&
                            (movement.CreatedByUserId == currentUserId ||
                             movement.Account!.CreatedByUserId == currentUserId ||
                             movement.Account.Members.Any(member => member.UserId == currentUserId)),
                asNoTracking: true,
                include: query => query
                    .Include(movement => movement.Account)
                        .ThenInclude(account => account!.Members)
                    .Include(movement => movement.PerformedByUser)
                    .Include(movement => movement.ReceiptLinks)
                        .ThenInclude(link => link.Receipt)
                            .ThenInclude(receipt => receipt!.Store)
                    .AsSplitQuery(),
                ct: ct);

            List<Receipt> availableReceipts = await unitOfWork.Receipt.GetItemsByPredicateAsync(
                receipt => receipt.DateTime >= dateFrom.Date &&
                           receipt.DateTime < periodEnd &&
                           !receipt.MoneyMovementLinks.Any() &&
                           (receipt.CreatedByUserId == currentUserId ||
                            receipt.Accounts.Any(link => link.Account!.CreatedByUserId == currentUserId) ||
                            receipt.Accounts.Any(link => link.Account!.Members.Any(member => member.UserId == currentUserId))),
                asNoTracking: true,
                include: query => query
                    .Include(receipt => receipt.Store)
                    .Include(receipt => receipt.Accounts)
                        .ThenInclude(link => link.Account)
                            .ThenInclude(account => account!.Members)
                    .Include(receipt => receipt.MoneyMovementLinks)
                    .AsSplitQuery(),
                ct: ct);

            List<MoneyMovementDto> result = movements
                .OrderByDescending(movement => movement.OccurredAt)
                .Select(movement => movement.MapDto(GetAvailableReceiptCount(movement, availableReceipts)))
                .ToList();

            return ServiceResult<List<MoneyMovementDto>>.Ok(result);
        }

        private static int GetAvailableReceiptCount(MoneyMovement movement, List<Receipt> availableReceipts)
        {
            return availableReceipts.Count(receipt =>
                receipt.DateTime.Date == movement.OccurredAt.Date &&
                receipt.TotalSum == movement.Amount &&
                (movement.AccountId == Guid.Empty
                    ? receipt.Accounts.Count == 0
                    : receipt.Accounts.Any(link => link.AccountId == movement.AccountId)));
        }
    }
}
