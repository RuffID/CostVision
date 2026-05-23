using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Domain.Models.Enums.Authorization;
using CostVision.Domain.Models.Receipts;
using Microsoft.EntityFrameworkCore;

namespace CostVision.Application.UseCases.Receipts.Accounts
{
    public class GetUserAccountsUseCase(IUnitOfWork unitOfWork) : IGetUserAccountsUseCase
    {
        public async Task<List<UserAccountViewModel>> ExecuteAsync(Guid userId, bool includeArchived, CancellationToken ct)
        {
            List<AccountMember> memberships = await unitOfWork.AccountMember.GetItemsByPredicateAsync(membership => membership.UserId == userId, asNoTracking: true, ct: ct);
            List<Guid> accountIds = memberships.Select(membership => membership.AccountId).Distinct().ToList();
            if (accountIds.Count == 0)
                return new List<UserAccountViewModel>();

            List<Account> accounts = await unitOfWork.Account.GetItemsByPredicateAsync(
                account => (includeArchived || !account.IsArchived) &&
                           (account.CreatedByUserId == userId || account.Members.Any(member => member.UserId == userId)),
                asNoTracking: true,
                include: query => query.Include(account => account.CreatedByUser),
                ct: ct);
            Dictionary<Guid, AccountAccessRole> rolesByAccountId = memberships
                .GroupBy(membership => membership.AccountId)
                .ToDictionary(group => group.Key, group => group.Select(membership => membership.Role).First());

            return accounts.Select(account => new UserAccountViewModel
            {
                Id = account.Id,
                Name = account.Name,
                Description = account.Description,
                ColorHex = account.ColorHex,
                IsActive = !account.IsArchived,
                CanManage = account.CreatedByUserId == userId,
                OwnerName = account.CreatedByUser!.Name,
                AccessRole = rolesByAccountId[account.Id]
            }).ToList();
        }
    }
}
