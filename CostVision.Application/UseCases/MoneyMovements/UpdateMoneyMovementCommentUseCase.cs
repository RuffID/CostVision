using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Models.Requests.MoneyMovements;
using CostVision.Application.Models.Responses.Results;
using CostVision.Domain.Models.MoneyMovements;
using CostVision.Domain.Models.Receipts;

namespace CostVision.Application.UseCases.MoneyMovements
{
    public class UpdateMoneyMovementCommentUseCase(IUnitOfWork unitOfWork) : IUpdateMoneyMovementCommentUseCase
    {
        public async Task<ServiceResult> ExecuteAsync(UpdateMoneyMovementCommentRequest request, Guid currentUserId, CancellationToken ct)
        {
            if (request.MoneyMovementId == Guid.Empty)
                return ServiceResult.Fail(ServiceErrorType.Validation, "Некорректный идентификатор операции.");

            if (request.AccountId.HasValue && request.AccountId.Value != Guid.Empty)
            {
                ServiceResult<AccountMember> access = await MoneyMovementAccountAccessValidator.GetEditableAccountMemberAsync(unitOfWork, request.AccountId.Value, currentUserId, ct);
                if (!access.Success)
                    return access.PropagateFailure();
            }

            MoneyMovement? movement = await unitOfWork.MoneyMovement.GetItemByPredicateAsync(
                item => item.Id == request.MoneyMovementId &&
                        (request.AccountId.HasValue && request.AccountId.Value != Guid.Empty
                            ? item.AccountId == request.AccountId.Value
                            : item.AccountId == Guid.Empty && item.CreatedByUserId == currentUserId),
                ct: ct);

            if (movement == null)
                return ServiceResult.Fail(ServiceErrorType.NotFound, "Операция не найдена в счёте.");

            if (!movement.TryUpdateComment(request.Comment, out string? error))
                return ServiceResult.Fail(ServiceErrorType.Validation, error!);

            await unitOfWork.SaveChangesAsync(ct);

            return ServiceResult.Ok();
        }
    }
}
