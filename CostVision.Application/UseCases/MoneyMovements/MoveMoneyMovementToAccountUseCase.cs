using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Models.Requests.MoneyMovements;
using CostVision.Application.Models.Responses.Results;
using CostVision.Domain.Models.MoneyMovements;
using CostVision.Domain.Models.Receipts;

namespace CostVision.Application.UseCases.MoneyMovements
{
    public class MoveMoneyMovementToAccountUseCase(IUnitOfWork unitOfWork) : IMoveMoneyMovementToAccountUseCase
    {
        public async Task<ServiceResult<bool>> ExecuteAsync(MoveMoneyMovementToAccountRequest request, Guid currentUserId, CancellationToken ct)
        {
            if (request.MoneyMovementId == Guid.Empty)
                return ServiceResult<bool>.Fail(400, "Некорректный идентификатор операции.");

            if (request.SourceAccountId == Guid.Empty || request.TargetAccountId == Guid.Empty)
                return ServiceResult<bool>.Fail(400, "Некорректный идентификатор счёта.");

            if (request.SourceAccountId == request.TargetAccountId)
                return ServiceResult<bool>.Fail(400, "Исходный и целевой счёт совпадают.");

            ServiceResult<AccountMember> sourceAccess = await MoneyMovementAccountAccessValidator.GetEditableAccountMemberAsync(unitOfWork, request.SourceAccountId, currentUserId, ct);
            if (!sourceAccess.Success)
                return ServiceResult<bool>.Fail(sourceAccess.Error!.StatusCode, sourceAccess.Error.Message);

            ServiceResult<AccountMember> targetAccess = await MoneyMovementAccountAccessValidator.GetEditableAccountMemberAsync(unitOfWork, request.TargetAccountId, currentUserId, ct);
            if (!targetAccess.Success)
                return ServiceResult<bool>.Fail(targetAccess.Error!.StatusCode, targetAccess.Error.Message);

            MoneyMovement? movement = await unitOfWork.MoneyMovement.GetItemByPredicateAsync(
                item => item.Id == request.MoneyMovementId && item.AccountId == request.SourceAccountId,
                ct: ct);

            if (movement == null)
                return ServiceResult<bool>.Fail(404, "Операция не найдена в исходном счёте.");

            movement.AccountId = request.TargetAccountId;
            await unitOfWork.SaveChangesAsync(ct);

            return ServiceResult<bool>.Ok(true);
        }
    }
}
