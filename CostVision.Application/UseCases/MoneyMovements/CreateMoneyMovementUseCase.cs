using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Models.Dtos.MoneyMovements;
using CostVision.Application.Models.Requests.MoneyMovements;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.MoneyMovements.Helpers;
using CostVision.Domain.Models.Enums.Authorization;
using CostVision.Domain.Models.Enums.MoneyMovements;
using CostVision.Domain.Models.Enums.Receipts;
using CostVision.Domain.Models.MoneyMovements;
using CostVision.Domain.Models.Receipts;
using Microsoft.EntityFrameworkCore;

namespace CostVision.Application.UseCases.MoneyMovements
{
    public class CreateMoneyMovementUseCase(IUnitOfWork unitOfWork) : ICreateMoneyMovementUseCase
    {
        public async Task<ServiceResult<MoneyMovementDto>> ExecuteAsync(CreateMoneyMovementRequest request, Guid currentUserId, CancellationToken ct)
        {
            Guid performedByUserId = request.PerformedByUserId.GetValueOrDefault(currentUserId);
            if (!MoneyMovement.TryCreateManual(
                    request.AccountId,
                    request.Amount,
                    request.Type,
                    request.OccurredAt,
                    request.Comment,
                    currentUserId,
                    performedByUserId,
                    DateTime.UtcNow,
                    out MoneyMovement? movement,
                    out string? error))
                return ServiceResult<MoneyMovementDto>.Fail(ServiceErrorType.Validation, error!);

            MoneyMovement newMovement = movement!;

            AccountMember? membership = await unitOfWork.AccountMember.GetItemByPredicateAsync(
                member => member.AccountId == request.AccountId && member.UserId == currentUserId,
                asNoTracking: true,
                ct: ct);

            if (membership == null)
                return ServiceResult<MoneyMovementDto>.Fail(ServiceErrorType.NotFound, "Счёт не найден или доступ к нему отсутствует.");

            if (membership.Role == AccountAccessRole.Viewer)
                return ServiceResult<MoneyMovementDto>.Fail(ServiceErrorType.Forbidden, "Недостаточно прав для добавления операции в этот счёт.");

            if (performedByUserId != currentUserId)
            {
                AccountMember? performedByMembership = await unitOfWork.AccountMember.GetItemByPredicateAsync(
                    member => member.AccountId == request.AccountId && member.UserId == performedByUserId,
                    asNoTracking: true,
                    ct: ct);

                if (performedByMembership == null)
                    return ServiceResult<MoneyMovementDto>.Fail(ServiceErrorType.Validation, "Исполнитель операции должен быть участником счёта.");
            }

            MoneyMovementDto? createdDto = null;
            await unitOfWork.ExecuteInTransaction(async transactionCt =>
            {
                unitOfWork.MoneyMovement.Create(newMovement);
                await unitOfWork.SaveChangesAsync(transactionCt);
                await TryAutoLinkExactReceiptAsync(newMovement, currentUserId, transactionCt);

                MoneyMovement? created = await unitOfWork.MoneyMovement.GetItemByPredicateAsync(
                    item => item.Id == newMovement.Id,
                    asNoTracking: true,
                    include: query => query
                        .Include(item => item.Account)
                        .Include(item => item.PerformedByUser),
                    ct: transactionCt);

                if (created == null)
                    throw new InvalidOperationException("Не удалось загрузить созданную операцию.");

                createdDto = created.MapDto();
            }, ct);

            return ServiceResult<MoneyMovementDto>.Ok(createdDto
                ?? throw new InvalidOperationException("Транзакция создания операции завершилась без результата."));
        }

        private async Task TryAutoLinkExactReceiptAsync(MoneyMovement movement, Guid currentUserId, CancellationToken ct)
        {
            if (movement.Type != MoneyMovementType.Expense)
                return;

            DateTime periodStart = movement.OccurredAt.Date;
            DateTime periodEnd = periodStart.AddDays(1);

            List<Receipt> receiptCandidates = await unitOfWork.Receipt.GetItemsByPredicateAsync(
                receipt => receipt.DateTime >= periodStart &&
                           receipt.DateTime < periodEnd &&
                           receipt.TotalSum == movement.Amount &&
                           receipt.OperationType == ReceiptOperationType.Income &&
                           !receipt.MoneyMovementLinks.Any() &&
                           receipt.Accounts.Any(link => link.AccountId == movement.AccountId) &&
                           (receipt.CreatedByUserId == currentUserId ||
                            receipt.Accounts.Any(link => link.Account!.CreatedByUserId == currentUserId) ||
                            receipt.Accounts.Any(link => link.Account!.Members.Any(member => member.UserId == currentUserId))),
                asNoTracking: true,
                include: query => query
                    .Include(receipt => receipt.Accounts)
                        .ThenInclude(link => link.Account)
                            .ThenInclude(account => account!.Members)
                    .Include(receipt => receipt.MoneyMovementLinks)
                    .AsSplitQuery(),
                ct: ct);

            if (receiptCandidates.Count != 1)
                return;

            List<MoneyMovement> movementCandidates = await unitOfWork.MoneyMovement.GetItemsByPredicateAsync(
                item => item.AccountId == movement.AccountId &&
                        item.OccurredAt >= periodStart &&
                        item.OccurredAt < periodEnd &&
                        item.Amount == movement.Amount &&
                        item.Type == MoneyMovementType.Expense &&
                        !item.ReceiptLinks.Any(),
                asNoTracking: true,
                include: query => query.Include(item => item.ReceiptLinks),
                ct: ct);

            if (movementCandidates.Count != 1 || movementCandidates[0].Id != movement.Id)
                return;

            if (!MoneyMovementReceipt.TryCreate(
                    movement.Id,
                    receiptCandidates[0].Id,
                    currentUserId,
                    DateTime.UtcNow,
                    out MoneyMovementReceipt? link,
                    out string? error))
                throw new InvalidOperationException(error!);

            unitOfWork.MoneyMovementReceipt.Create(link!);
        }
    }
}
