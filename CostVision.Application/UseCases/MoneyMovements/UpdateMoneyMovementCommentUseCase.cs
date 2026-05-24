using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Models.Requests.MoneyMovements;
using CostVision.Application.Models.Responses.Results;
using CostVision.Domain.Models.MoneyMovements;
using CostVision.Domain.Models.Receipts;

namespace CostVision.Application.UseCases.MoneyMovements
{
    public class UpdateMoneyMovementCommentUseCase(IUnitOfWork unitOfWork) : IUpdateMoneyMovementCommentUseCase
    {
        private const int COMMENT_MAX_LENGTH = 1024;

        public async Task<ServiceResult<bool>> ExecuteAsync(UpdateMoneyMovementCommentRequest request, Guid currentUserId, CancellationToken ct)
        {
            if (request.MoneyMovementId == Guid.Empty)
                return ServiceResult<bool>.Fail(400, "Некорректный идентификатор операции.");

            ServiceResult<AccountMember> access = await MoneyMovementAccountAccessValidator.GetEditableAccountMemberAsync(unitOfWork, request.AccountId, currentUserId, ct);
            if (!access.Success)
                return ServiceResult<bool>.Fail(access.Error!.StatusCode, access.Error.Message);

            string? comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
            if (comment?.Length > COMMENT_MAX_LENGTH)
                return ServiceResult<bool>.Fail(400, "Комментарий не должен превышать 1024 символа.");

            MoneyMovement? movement = await unitOfWork.MoneyMovement.GetItemByPredicateAsync(
                item => item.Id == request.MoneyMovementId && item.AccountId == request.AccountId,
                ct: ct);

            if (movement == null)
                return ServiceResult<bool>.Fail(404, "Операция не найдена в счёте.");

            movement.Comment = comment;
            await unitOfWork.SaveChangesAsync(ct);

            return ServiceResult<bool>.Ok(true);
        }
    }
}
