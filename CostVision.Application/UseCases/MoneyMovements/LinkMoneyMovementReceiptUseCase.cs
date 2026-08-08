using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Models.Requests.MoneyMovements;
using CostVision.Application.Models.Responses.Results;
using CostVision.Domain.Models.MoneyMovements;
using CostVision.Domain.Models.Receipts;
using Microsoft.EntityFrameworkCore;

namespace CostVision.Application.UseCases.MoneyMovements
{
    public class LinkMoneyMovementReceiptUseCase(IUnitOfWork unitOfWork) : ILinkMoneyMovementReceiptUseCase
    {
        public async Task<ServiceResult> ExecuteAsync(LinkMoneyMovementReceiptRequest request, Guid currentUserId, CancellationToken ct)
        {
            if (request.MoneyMovementId == Guid.Empty || request.ReceiptId == Guid.Empty)
                return ServiceResult.Fail(ServiceErrorType.Validation, "Некорректный идентификатор операции или чека.");

            MoneyMovement? movement = await unitOfWork.MoneyMovement.GetItemByIdAsync(request.MoneyMovementId, asNoTracking: true, ct: ct);
            if (movement == null)
                return ServiceResult.Fail(ServiceErrorType.NotFound, "Операция не найдена.");

            ServiceResult<AccountMember> access = await MoneyMovementAccountAccessValidator.GetEditableAccountMemberAsync(unitOfWork, movement.AccountId, currentUserId, ct);
            if (!access.Success)
                return access.PropagateFailure();

            Receipt? receipt = await unitOfWork.Receipt.GetItemByPredicateAsync(
                item => item.Id == request.ReceiptId &&
                        (item.CreatedByUserId == currentUserId ||
                         item.Accounts.Any(link => link.Account!.CreatedByUserId == currentUserId) ||
                         item.Accounts.Any(link => link.Account!.Members.Any(member => member.UserId == currentUserId))) &&
                        (item.Accounts.Any(link => link.AccountId == movement.AccountId) ||
                         (!item.Accounts.Any() && item.CreatedByUserId == currentUserId)),
                asNoTracking: true,
                include: query => query
                    .Include(item => item.Accounts)
                        .ThenInclude(link => link.Account)
                            .ThenInclude(account => account!.Members)
                    .AsSplitQuery(),
                ct: ct);

            if (receipt == null)
                return ServiceResult.Fail(ServiceErrorType.NotFound, "Чек не найден или доступ к нему отсутствует.");

            MoneyMovementReceipt? existingLink = await unitOfWork.MoneyMovementReceipt.GetItemByPredicateAsync(
                link => link.MoneyMovementId == request.MoneyMovementId && link.ReceiptId == request.ReceiptId,
                asNoTracking: true,
                ct: ct);

            if (existingLink != null)
                return ServiceResult.Fail(ServiceErrorType.Conflict, "Чек уже привязан к операции.");

            if (!MoneyMovementReceipt.TryCreate(
                    request.MoneyMovementId,
                    request.ReceiptId,
                    currentUserId,
                    DateTime.UtcNow,
                    out MoneyMovementReceipt? link,
                    out string? error))
                return ServiceResult.Fail(ServiceErrorType.Validation, error!);

            await unitOfWork.ExecuteInTransaction(() =>
            {
                unitOfWork.MoneyMovementReceipt.Create(link!);

                return Task.CompletedTask;
            }, ct);

            return ServiceResult.Ok();
        }
    }
}
