using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Models.Requests.MoneyMovements;
using CostVision.Application.Models.Responses.Results;
using CostVision.Domain.Models.MoneyMovements;
using CostVision.Domain.Models.Receipts;

namespace CostVision.Application.UseCases.MoneyMovements
{
    public class UnlinkMoneyMovementReceiptUseCase(IUnitOfWork unitOfWork) : IUnlinkMoneyMovementReceiptUseCase
    {
        public async Task<ServiceResult<bool>> ExecuteAsync(UnlinkMoneyMovementReceiptRequest request, Guid currentUserId, CancellationToken ct)
        {
            if (request.MoneyMovementId == Guid.Empty || request.ReceiptId == Guid.Empty)
                return ServiceResult<bool>.Fail(400, "Некорректный идентификатор операции или чека.");

            MoneyMovement? movement = await unitOfWork.MoneyMovement.GetItemByIdAsync(request.MoneyMovementId, asNoTracking: true, ct: ct);
            if (movement == null)
                return ServiceResult<bool>.Fail(404, "Операция не найдена.");

            ServiceResult<AccountMember> access = await MoneyMovementAccountAccessValidator.GetEditableAccountMemberAsync(unitOfWork, movement.AccountId, currentUserId, ct);
            if (!access.Success)
                return ServiceResult<bool>.Fail(access.Error!.StatusCode, access.Error.Message);

            MoneyMovementReceipt? link = await unitOfWork.MoneyMovementReceipt.GetItemByPredicateAsync(
                item => item.MoneyMovementId == request.MoneyMovementId && item.ReceiptId == request.ReceiptId,
                ct: ct);

            if (link == null)
                return ServiceResult<bool>.Fail(404, "Связь операции и чека не найдена.");

            await unitOfWork.ExecuteInTransaction(() =>
            {
                unitOfWork.MoneyMovementReceipt.Delete(link);
                return Task.CompletedTask;
            }, ct);

            return ServiceResult<bool>.Ok(true);
        }
    }
}
