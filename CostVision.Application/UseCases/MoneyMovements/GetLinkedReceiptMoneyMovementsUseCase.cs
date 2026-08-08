using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Models.Dtos.MoneyMovements;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.MoneyMovements.Helpers;
using CostVision.Domain.Models.MoneyMovements;
using CostVision.Domain.Models.Receipts;
using Microsoft.EntityFrameworkCore;

namespace CostVision.Application.UseCases.MoneyMovements
{
    public class GetLinkedReceiptMoneyMovementsUseCase(IUnitOfWork unitOfWork) : IGetLinkedReceiptMoneyMovementsUseCase
    {
        public async Task<ServiceResult<List<ReceiptMoneyMovementDto>>> ExecuteAsync(Guid receiptId, Guid currentUserId, CancellationToken ct)
        {
            if (receiptId == Guid.Empty)
                return ServiceResult<List<ReceiptMoneyMovementDto>>.Fail(ServiceErrorType.Validation, "Некорректный идентификатор чека.");

            Receipt? receipt = await unitOfWork.Receipt.GetItemByPredicateAsync(
                item => item.Id == receiptId &&
                        (item.CreatedByUserId == currentUserId ||
                         item.Accounts.Any(link => link.Account!.CreatedByUserId == currentUserId) ||
                         item.Accounts.Any(link => link.Account!.Members.Any(member => member.UserId == currentUserId))),
                asNoTracking: true,
                include: query => query
                    .Include(item => item.Accounts)
                        .ThenInclude(link => link.Account)
                            .ThenInclude(account => account!.Members)
                    .AsSplitQuery(),
                ct: ct);

            if (receipt == null)
                return ServiceResult<List<ReceiptMoneyMovementDto>>.Fail(ServiceErrorType.NotFound, "Чек не найден или доступ к нему отсутствует.");

            List<MoneyMovementReceipt> links = await unitOfWork.MoneyMovementReceipt.GetItemsByPredicateAsync(
                link => link.ReceiptId == receiptId,
                asNoTracking: true,
                include: query => query
                    .Include(link => link.MoneyMovement)
                        .ThenInclude(movement => movement!.Account)
                    .AsSplitQuery(),
                ct: ct);

            List<ReceiptMoneyMovementDto> result = links
                .Where(link => link.MoneyMovement != null)
                .OrderByDescending(link => link.MoneyMovement!.OccurredAt)
                .Select(link => link.MapReceiptMoneyMovementDto())
                .ToList();

            return ServiceResult<List<ReceiptMoneyMovementDto>>.Ok(result);
        }
    }
}
