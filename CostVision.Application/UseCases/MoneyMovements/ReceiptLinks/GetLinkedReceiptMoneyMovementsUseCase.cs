using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Models.Dtos.MoneyMovements;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.MoneyMovements.Helpers;
using CostVision.Domain.Models.MoneyMovements;
using CostVision.Domain.Models.Receipts;
using Microsoft.EntityFrameworkCore;
using CostVision.Application.Extensions;
using System.Linq.Expressions;

namespace CostVision.Application.UseCases.MoneyMovements
{
    public class GetLinkedReceiptMoneyMovementsUseCase(IUnitOfWork unitOfWork) : IGetLinkedReceiptMoneyMovementsUseCase
    {
        public async Task<ServiceResult<List<ReceiptMoneyMovementDto>>> ExecuteAsync(Guid receiptId, Guid currentUserId, CancellationToken ct)
        {
            if (receiptId == Guid.Empty)
            {
                return ServiceResult<List<ReceiptMoneyMovementDto>>.Fail(ServiceErrorType.Validation, "Некорректный идентификатор чека.");
            }

            Expression<Func<Receipt, bool>> receiptPredicate = item => item.Id == receiptId;

            Receipt? receipt = await unitOfWork.Receipt.GetItemByPredicateAsync(
                receiptPredicate.WithReceiptReadAccess(item => item, currentUserId),
                asNoTracking: true,
                include: query => query
                    .Include(item => item.Accounts)
                        .ThenInclude(link => link.Account)
                            .ThenInclude(account => account!.Members)
                    .AsSplitQuery(),
                ct: ct);

            if (receipt == null)
            {
                return ServiceResult<List<ReceiptMoneyMovementDto>>.Fail(ServiceErrorType.NotFound, "Чек не найден или доступ к нему отсутствует.");
            }

            Expression<Func<MoneyMovementReceipt, bool>> linkPredicate = link => link.ReceiptId == receiptId && link.MoneyMovement != null;
            List<MoneyMovementReceipt> links = await unitOfWork.MoneyMovementReceipt.GetItemsByPredicateAsync(
                linkPredicate.WithAccountReadAccess(link => link.MoneyMovement!.Account, currentUserId),
                asNoTracking: true,
                include: query => query
                    .Include(link => link.MoneyMovement)
                        .ThenInclude(movement => movement!.Account)
                            .ThenInclude(account => account!.Members)
                    .AsSplitQuery(),
                ct: ct);

            List<ReceiptMoneyMovementDto> result = links
                .Where(link => link.MoneyMovement != null)
                .OrderByDescending(link => link.MoneyMovement!.OccurredAt)
                .Select(link => link.MapReceiptMoneyMovementDto(currentUserId))
                .ToList();

            return ServiceResult<List<ReceiptMoneyMovementDto>>.Ok(result);
        }
    }
}
