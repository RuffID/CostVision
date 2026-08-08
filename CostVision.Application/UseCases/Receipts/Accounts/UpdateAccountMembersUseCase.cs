using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.Enums.Authorization;
using CostVision.Domain.Models.Receipts;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;
using Microsoft.EntityFrameworkCore;

namespace CostVision.Application.UseCases.Receipts.Accounts
{
    public class UpdateAccountMembersUseCase(IUnitOfWork unitOfWork) : IUpdateAccountMembersUseCase
    {
        public async Task<ServiceResult> ExecuteAsync(Guid accountId, Guid ownerUserId, IReadOnlyCollection<UpdateAccountMemberRequest> members, CancellationToken ct)
        {
            if (accountId == Guid.Empty)
                return ServiceResult.Fail(ServiceErrorType.Validation, "Некорректный идентификатор счёта.");

            Account? account = await unitOfWork.Account.GetItemByPredicateAsync(
                account => account.Id == accountId && account.CreatedByUserId == ownerUserId,
                asNoTracking: false,
                include: query => query.Include(account => account.Members),
                ct: ct);
            if (account == null)
                return ServiceResult.Fail(ServiceErrorType.NotFound, "Счёт не найден.");

            List<UpdateAccountMemberRequest> desiredMembers = members
                .GroupBy(member => member.UserId)
                .Select(group => group.Last())
                .ToList();

            foreach (UpdateAccountMemberRequest member in desiredMembers)
            {
                if (!account.CanAssignMember(member.UserId, member.Role, out string? error))
                    return ServiceResult.Fail(ServiceErrorType.Validation, error ?? "Некорректные данные участника счёта.");
            }

            HashSet<Guid> desiredUserIds = desiredMembers.Select(member => member.UserId).ToHashSet();
            List<User> availableUsers = desiredUserIds.Count == 0
                ? new List<User>()
                : await unitOfWork.User.GetItemsByPredicateAsync(user => desiredUserIds.Contains(user.Id) && user.IsActive, asNoTracking: true, ct: ct);

            if (availableUsers.Count != desiredUserIds.Count)
                return ServiceResult.Fail(ServiceErrorType.Validation, "Один или несколько выбранных пользователей недоступны.");

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

            List<AccountMember> removedMembers = new();
            foreach (AccountMember member in membersToRemove)
            {
                if (!account.TryRemoveMember(member.UserId, out AccountMember? removedMember, out string? error) || removedMember == null)
                    return ServiceResult.Fail(ServiceErrorType.Validation, error ?? "Не удалось удалить участника счёта.");

                removedMembers.Add(removedMember);
            }

            foreach (AccountMember member in membersToUpdate)
            {
                AccountAccessRole desiredRole = desiredRolesByUserId[member.UserId];
                if (member.Role != desiredRole && !account.TryChangeMemberRole(member.UserId, desiredRole, out string? error))
                    return ServiceResult.Fail(ServiceErrorType.Validation, error ?? "Не удалось изменить роль участника счёта.");
            }

            List<AccountMember> addedMembers = new();
            foreach (Guid userId in userIdsToAdd)
            {
                if (!account.TryAddMember(userId, desiredRolesByUserId[userId], out AccountMember? addedMember, out string? error) || addedMember == null)
                    return ServiceResult.Fail(ServiceErrorType.Validation, error ?? "Не удалось добавить участника счёта.");

                addedMembers.Add(addedMember);
            }

            await unitOfWork.ExecuteInTransaction(_ =>
            {
                if (removedMembers.Count > 0)
                    unitOfWork.AccountMember.DeleteRange(removedMembers);

                if (addedMembers.Count > 0)
                    unitOfWork.AccountMember.CreateRange(addedMembers);

                return Task.CompletedTask;
            }, ct);

            return ServiceResult.Ok();
        }
    }
}
