using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Models.Dtos.MoneyMovements;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.MoneyMovements.Helpers;
using CostVision.Domain.Models.MoneyMovements;
using Microsoft.EntityFrameworkCore;

namespace CostVision.Application.UseCases.MoneyMovements
{
    public class GetLinkedMoneyMovementReceiptsUseCase(IUnitOfWork unitOfWork) : IGetLinkedMoneyMovementReceiptsUseCase
    {
        public async Task<ServiceResult<List<MoneyMovementReceiptDto>>> ExecuteAsync(Guid moneyMovementId, Guid currentUserId, CancellationToken ct)
        {
            if (moneyMovementId == Guid.Empty)
                return ServiceResult<List<MoneyMovementReceiptDto>>.Fail(400, "Некорректный идентификатор операции.");

            MoneyMovement? movement = await unitOfWork.MoneyMovement.GetItemByPredicateAsync(
                item => item.Id == moneyMovementId &&
                        (item.Account!.CreatedByUserId == currentUserId ||
                         item.Account.Members.Any(member => member.UserId == currentUserId)),
                asNoTracking: true,
                include: query => query
                    .Include(item => item.Account)
                        .ThenInclude(account => account!.Members),
                ct: ct);

            if (movement == null)
                return ServiceResult<List<MoneyMovementReceiptDto>>.Fail(404, "Операция не найдена или доступ к ней отсутствует.");

            List<MoneyMovementReceipt> links = await unitOfWork.MoneyMovementReceipt.GetItemsByPredicateAsync(
                link => link.MoneyMovementId == moneyMovementId,
                asNoTracking: true,
                include: query => query
                    .Include(link => link.Receipt)
                        .ThenInclude(receipt => receipt!.Store)
                    .Include(link => link.Receipt)
                        .ThenInclude(receipt => receipt!.Accounts)
                            .ThenInclude(link => link.Account)
                    .AsSplitQuery(),
                ct: ct);

            List<MoneyMovementReceiptDto> result = links
                .Where(link => link.Receipt != null)
                .OrderByDescending(link => link.Receipt!.DateTime)
                .Select(link => link.MapReceiptLinkDto())
                .ToList();

            return ServiceResult<List<MoneyMovementReceiptDto>>.Ok(result);
        }
    }
}
