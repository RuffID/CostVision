using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.Enums.Authorization;
using CostVision.Domain.Models.Receipts;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;

namespace CostVision.Application.UseCases.Receipts.Accounts
{
    public class UpdateAccountMembersUseCase(IUnitOfWork unitOfWork) : IUpdateAccountMembersUseCase
    {
        public async Task<ServiceResult<bool>> ExecuteAsync(Guid accountId, Guid ownerUserId, IReadOnlyCollection<UpdateAccountMemberRequest> members, CancellationToken ct)
        {
            if (accountId == Guid.Empty)
                return ServiceResult<bool>.Fail(400, "Некорректный идентификатор счёта.");

            Account? account = await unitOfWork.Account.GetOwnedByIdWithMembersAsync(accountId, ownerUserId, asNoTracking: false, ct: ct);
            if (account == null)
                return ServiceResult<bool>.Fail(404, "Счёт не найден.");

            List<UpdateAccountMemberRequest> desiredMembers = members
                .Where(member => member.UserId != Guid.Empty && member.UserId != ownerUserId)
                .GroupBy(member => member.UserId)
                .Select(group => group.Last())
                .ToList();

            if (desiredMembers.Any(member => !Enum.IsDefined(typeof(AccountAccessRole), member.Role) || member.Role == AccountAccessRole.Owner))
                return ServiceResult<bool>.Fail(400, "Можно назначить только роли Viewer или Editor.");

            List<Guid> desiredUserIds = desiredMembers.Select(member => member.UserId).ToList();
            List<User> availableUsers = desiredUserIds.Count == 0
                ? new List<User>()
                : await unitOfWork.User.GetItemsByPredicateAsync(user => desiredUserIds.Contains(user.Id) && user.IsActive, asNoTracking: true, ct: ct);

            if (availableUsers.Count != desiredUserIds.Count)
                return ServiceResult<bool>.Fail(400, "Один или несколько выбранных пользователей недоступны.");

            List<AccountMember> currentMembers = account.Members
                .Where(member => member.UserId != ownerUserId)
                .ToList();

            HashSet<Guid> currentUserIds = currentMembers
                .Select(member => member.UserId)
                .ToHashSet();

            List<AccountMember> membersToRemove = currentMembers
                .Where(member => !desiredUserIds.Contains(member.UserId))
                .ToList();

            List<Guid> userIdsToAdd = desiredUserIds
                .Where(userId => !currentUserIds.Contains(userId))
                .ToList();

            List<AccountMember> membersToUpdate = currentMembers
                .Where(member => desiredUserIds.Contains(member.UserId))
                .ToList();

            Dictionary<Guid, AccountAccessRole> desiredRolesByUserId = desiredMembers
                .ToDictionary(member => member.UserId, member => member.Role);

            await unitOfWork.ExecuteInTransaction(async () =>
            {
                if (membersToRemove.Count > 0)
                    unitOfWork.AccountMember.DeleteRange(membersToRemove);

                foreach (AccountMember member in membersToUpdate)
                {
                    AccountAccessRole desiredRole = desiredRolesByUserId[member.UserId];
                    if (member.Role != desiredRole)
                        member.Role = desiredRole;
                }

                foreach (Guid userId in userIdsToAdd)
                {
                    unitOfWork.AccountMember.Create(new AccountMember
                    {
                        AccountId = accountId,
                        UserId = userId,
                        Role = desiredRolesByUserId[userId]
                    });
                }

                await Task.CompletedTask;
            }, ct);

            return ServiceResult<bool>.Ok(true);
        }
    }
}
