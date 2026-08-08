using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Models.Requests.MoneyMovements;
using CostVision.Application.Models.Responses.Results;
using CostVision.Domain.Models.MoneyMovements;
using CostVision.Domain.Models.Receipts;

namespace CostVision.Application.UseCases.MoneyMovements
{
    public class UnlinkMoneyMovementReceiptUseCase(IUnitOfWork unitOfWork) : IUnlinkMoneyMovementReceiptUseCase
    {
        public async Task<ServiceResult> ExecuteAsync(UnlinkMoneyMovementReceiptRequest request, Guid currentUserId, CancellationToken ct)
        {
            if (request.MoneyMovementId == Guid.Empty || request.ReceiptId == Guid.Empty)
                return ServiceResult.Fail(ServiceErrorType.Validation, "Некорректный идентификатор операции или чека.");

            MoneyMovement? movement = await unitOfWork.MoneyMovement.GetItemByIdAsync(request.MoneyMovementId, asNoTracking: true, ct: ct);
            if (movement == null)
                return ServiceResult.Fail(ServiceErrorType.NotFound, "Операция не найдена.");

            ServiceResult<AccountMember> access = await MoneyMovementAccountAccessValidator.GetEditableAccountMemberAsync(unitOfWork, movement.AccountId, currentUserId, ct);
            if (!access.Success)
                return access.PropagateFailure();

            MoneyMovementReceipt? link = await unitOfWork.MoneyMovementReceipt.GetItemByPredicateAsync(
                item => item.MoneyMovementId == request.MoneyMovementId && item.ReceiptId == request.ReceiptId,
                ct: ct);

            if (link == null)
                return ServiceResult.Fail(ServiceErrorType.NotFound, "Связь операции и чека не найдена.");

            await unitOfWork.ExecuteInTransaction(() =>
            {
                unitOfWork.MoneyMovementReceipt.Delete(link);
                return Task.CompletedTask;
            }, ct);

            return ServiceResult.Ok();
        }
    }
}
