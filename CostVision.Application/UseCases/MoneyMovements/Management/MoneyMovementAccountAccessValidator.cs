using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Models.Responses.Results;
using CostVision.Domain.Models.Enums.Authorization;
using CostVision.Domain.Models.Receipts;
using Microsoft.EntityFrameworkCore;

namespace CostVision.Application.UseCases.MoneyMovements
{
    internal static class MoneyMovementAccountAccessValidator
    {
        public static async Task<ServiceResult<AccountMember>> GetEditableAccountMemberAsync(IUnitOfWork unitOfWork, Guid accountId, Guid currentUserId, CancellationToken ct, bool requireActive = false)
        {
            if (accountId == Guid.Empty)
            {
                return ServiceResult<AccountMember>.Fail(ServiceErrorType.Validation, "Некорректный идентификатор счёта.");
            }

            Func<IQueryable<AccountMember>, IQueryable<AccountMember>>? include = null;
            if (requireActive)
            {
                include = query => query.Include(member => member.Account);
            }

            AccountMember? membership = await unitOfWork.AccountMember.GetItemByPredicateAsync(
                member => member.AccountId == accountId && member.UserId == currentUserId,
                asNoTracking: true,
                include: include,
                ct: ct);

            if (membership == null)
            {
                return ServiceResult<AccountMember>.Fail(ServiceErrorType.NotFound, "Счёт не найден или доступ к нему отсутствует.");
            }

            if (membership.Role != AccountAccessRole.Owner && membership.Role != AccountAccessRole.Editor)
            {
                return ServiceResult<AccountMember>.Fail(ServiceErrorType.Forbidden, "Недостаточно прав для изменения операций в этом счёте.");
            }

            if (requireActive && membership.Account == null)
            {
                throw new InvalidOperationException("Не загружен счёт для проверки архива.");
            }

            if (requireActive && membership.Account.IsArchived)
            {
                return ServiceResult<AccountMember>.Fail(ServiceErrorType.Forbidden, "Нельзя добавлять операции или сохранять импорт в архивный счёт.");
            }

            return ServiceResult<AccountMember>.Ok(membership);
        }
    }
}
