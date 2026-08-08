using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Domain.Models.Enums.Authorization;
using CostVision.Domain.Models.Receipts;
using CostVision.Application.Models.Responses.Results;

namespace CostVision.Application.UseCases.Receipts.Accounts
{
    public class ValidateReceiptCreationAccessUseCase(IUnitOfWork unitOfWork) : IValidateReceiptCreationAccessUseCase
    {
        public async Task<ServiceResult> ExecuteAsync(Guid accountId, Guid userId, CancellationToken ct)
        {
            if (accountId == Guid.Empty)
                return ServiceResult.Fail(ServiceErrorType.Validation, "Некорректный идентификатор счёта.");

            AccountMember? membership = await unitOfWork.AccountMember.GetItemByPredicateAsync(
                member => member.AccountId == accountId && member.UserId == userId,
                asNoTracking: true,
                ct: ct);

            if (membership == null)
                return ServiceResult.Fail(ServiceErrorType.NotFound, "Счёт не найден или доступ к нему отсутствует.");

            if (membership.Role == AccountAccessRole.Viewer)
                return ServiceResult.Fail(ServiceErrorType.Forbidden, "Недостаточно прав для добавления чеков в этот счёт.");

            return ServiceResult.Ok();
        }
    }
}
