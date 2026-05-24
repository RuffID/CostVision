using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Models.Requests.MoneyMovements;
using CostVision.Application.Models.Responses.Results;
using CostVision.Domain.Models.MoneyMovements;
using CostVision.Domain.Models.Receipts;

namespace CostVision.Application.UseCases.MoneyMovements
{
    public class DeleteMoneyMovementUseCase(IUnitOfWork unitOfWork) : IDeleteMoneyMovementUseCase
    {
        public async Task<ServiceResult<bool>> ExecuteAsync(DeleteMoneyMovementRequest request, Guid currentUserId, CancellationToken ct)
        {
            if (request.MoneyMovementId == Guid.Empty)
                return ServiceResult<bool>.Fail(400, "Некорректный идентификатор операции.");

            ServiceResult<AccountMember> access = await MoneyMovementAccountAccessValidator.GetEditableAccountMemberAsync(unitOfWork, request.AccountId, currentUserId, ct);
            if (!access.Success)
                return ServiceResult<bool>.Fail(access.Error!.StatusCode, access.Error.Message);

            MoneyMovement? movement = await unitOfWork.MoneyMovement.GetItemByPredicateAsync(
                item => item.Id == request.MoneyMovementId && item.AccountId == request.AccountId,
                ct: ct);

            if (movement == null)
                return ServiceResult<bool>.Fail(404, "Операция не найдена в счёте.");

            unitOfWork.MoneyMovement.Delete(movement);
            await unitOfWork.SaveChangesAsync(ct);

            return ServiceResult<bool>.Ok(true);
        }
    }
}
