using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Models.Dtos.MoneyMovements;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.MoneyMovements.Helpers;
using CostVision.Domain.Models.MoneyMovements;
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
                            (movement.Account!.CreatedByUserId == currentUserId ||
                             movement.Account.Members.Any(member => member.UserId == currentUserId)),
                asNoTracking: true,
                include: query => query
                    .Include(movement => movement.Account)
                        .ThenInclude(account => account!.Members)
                    .Include(movement => movement.PerformedByUser)
                    .Include(movement => movement.ReceiptLinks)
                        .ThenInclude(link => link.Receipt)
                    .AsSplitQuery(),
                ct: ct);

            List<MoneyMovementDto> result = movements
                .OrderByDescending(movement => movement.OccurredAt)
                .Select(movement => movement.MapDto())
                .ToList();

            return ServiceResult<List<MoneyMovementDto>>.Ok(result);
        }
    }
}
