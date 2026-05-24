using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Models.Responses.Results;
using CostVision.Domain.Models.Enums.Authorization;
using CostVision.Domain.Models.Receipts;

namespace CostVision.Application.UseCases.MoneyMovements
{
    internal static class MoneyMovementAccountAccessValidator
    {
        public static async Task<ServiceResult<AccountMember>> GetEditableAccountMemberAsync(IUnitOfWork unitOfWork, Guid accountId, Guid currentUserId, CancellationToken ct)
        {
            if (accountId == Guid.Empty)
                return ServiceResult<AccountMember>.Fail(400, "Некорректный идентификатор счёта.");

            AccountMember? membership = await unitOfWork.AccountMember.GetItemByPredicateAsync(
                member => member.AccountId == accountId && member.UserId == currentUserId,
                asNoTracking: true,
                ct: ct);

            if (membership == null)
                return ServiceResult<AccountMember>.Fail(404, "Счёт не найден или доступ к нему отсутствует.");

            if (membership.Role == AccountAccessRole.Viewer)
                return ServiceResult<AccountMember>.Fail(403, "Недостаточно прав для изменения операций в этом счёте.");

            return ServiceResult<AccountMember>.Ok(membership);
        }
    }
}
