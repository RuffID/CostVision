using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.MoneyMovements;
using CostVision.Application.Models.Dtos.MoneyMovements;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.MoneyMovements.Helpers;
using CostVision.Domain.Models.MoneyMovements;
using Microsoft.EntityFrameworkCore;
using CostVision.Application.Extensions;
using System.Linq.Expressions;

namespace CostVision.Application.UseCases.MoneyMovements
{
    public class GetLinkedMoneyMovementReceiptsUseCase(IMoneyMovementRepository moneyMovementRepository, IMoneyMovementReceiptRepository moneyMovementReceiptRepository) : IGetLinkedMoneyMovementReceiptsUseCase
    {
        public async Task<ServiceResult<List<MoneyMovementReceiptDto>>> ExecuteAsync(Guid moneyMovementId, Guid currentUserId, CancellationToken ct)
        {
            if (moneyMovementId == Guid.Empty)
            {
                return ServiceResult<List<MoneyMovementReceiptDto>>.Fail(ServiceErrorType.Validation, "Некорректный идентификатор операции.");
            }

            Expression<Func<MoneyMovement, bool>> movementPredicate = item => item.Id == moneyMovementId;

            MoneyMovement? movement = await moneyMovementRepository.GetItemByPredicateAsync(
                movementPredicate.WithAccountReadAccess(item => item.Account, currentUserId),
                asNoTracking: true,
                include: query => query
                    .Include(item => item.Account)
                        .ThenInclude(account => account!.Members),
                ct: ct);

            if (movement == null)
            {
                return ServiceResult<List<MoneyMovementReceiptDto>>.Fail(ServiceErrorType.NotFound, "Операция не найдена или доступ к ней отсутствует.");
            }

            Expression<Func<MoneyMovementReceipt, bool>> linkPredicate = link => link.MoneyMovementId == moneyMovementId;
            List<MoneyMovementReceipt> links = await moneyMovementReceiptRepository.GetItemsByPredicateAsync(
                linkPredicate.WithReceiptReadAccess(link => link.Receipt, currentUserId),
                asNoTracking: true,
                include: query => query
                    .Include(link => link.Receipt)
                        .ThenInclude(receipt => receipt!.Store)
                    .Include(link => link.Receipt)
                        .ThenInclude(receipt => receipt!.CreatedByUser)
                    .Include(link => link.Receipt)
                        .ThenInclude(receipt => receipt!.Accounts)
                            .ThenInclude(link => link.Account)
                                .ThenInclude(account => account!.Members)
                    .AsSplitQuery(),
                ct: ct);

            List<MoneyMovementReceiptDto> result = links
                .Where(link => link.Receipt != null)
                .OrderByDescending(link => link.Receipt!.DateTime)
                .Select(link => link.MapReceiptLinkDto(currentUserId))
                .ToList();

            return ServiceResult<List<MoneyMovementReceiptDto>>.Ok(result);
        }
    }
}
