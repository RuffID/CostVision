using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Models.Requests.MoneyMovements;
using CostVision.Application.Models.Responses.Results;
using CostVision.Domain.Models.MoneyMovements;
using CostVision.Domain.Models.Receipts;

namespace CostVision.Application.UseCases.MoneyMovements
{
    public class MoveMoneyMovementToAccountUseCase(IUnitOfWork unitOfWork) : IMoveMoneyMovementToAccountUseCase
    {
        public async Task<ServiceResult> ExecuteAsync(MoveMoneyMovementToAccountRequest request, Guid currentUserId, CancellationToken ct)
        {
            if (request.MoneyMovementId == Guid.Empty)
                return ServiceResult.Fail(ServiceErrorType.Validation, "Некорректный идентификатор операции.");

            if (request.TargetAccountId == Guid.Empty)
                return ServiceResult.Fail(ServiceErrorType.Validation, "Некорректный идентификатор счёта.");

            if (request.SourceAccountId != Guid.Empty && request.SourceAccountId == request.TargetAccountId)
                return ServiceResult.Fail(ServiceErrorType.Validation, "Исходный и целевой счёт совпадают.");

            if (request.SourceAccountId != Guid.Empty)
            {
                ServiceResult<AccountMember> sourceAccess = await MoneyMovementAccountAccessValidator.GetEditableAccountMemberAsync(unitOfWork, request.SourceAccountId, currentUserId, ct);
                if (!sourceAccess.Success)
                    return sourceAccess.PropagateFailure();
            }

            ServiceResult<AccountMember> targetAccess = await MoneyMovementAccountAccessValidator.GetEditableAccountMemberAsync(unitOfWork, request.TargetAccountId, currentUserId, ct);
            if (!targetAccess.Success)
                return targetAccess.PropagateFailure();

            MoneyMovement? movement = await unitOfWork.MoneyMovement.GetItemByPredicateAsync(
                item => item.Id == request.MoneyMovementId &&
                        (request.SourceAccountId != Guid.Empty
                            ? item.AccountId == request.SourceAccountId
                            : item.AccountId == Guid.Empty && item.CreatedByUserId == currentUserId),
                ct: ct);

            if (movement == null)
                return ServiceResult.Fail(ServiceErrorType.NotFound, "Операция не найдена в исходном счёте.");

            if (!movement.TryMoveToAccount(request.TargetAccountId, out string? error))
                return ServiceResult.Fail(ServiceErrorType.Validation, error!);

            await unitOfWork.SaveChangesAsync(ct);

            return ServiceResult.Ok();
        }
    }
}
