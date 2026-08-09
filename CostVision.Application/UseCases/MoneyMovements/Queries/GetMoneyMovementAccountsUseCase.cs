using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Domain.Models.Enums.Authorization;
using CostVision.Domain.Models.Receipts;
using Microsoft.EntityFrameworkCore;

namespace CostVision.Application.UseCases.MoneyMovements
{
    public class GetMoneyMovementAccountsUseCase(IUnitOfWork unitOfWork) : IGetMoneyMovementAccountsUseCase
    {
        public async Task<List<UserAccountViewModel>> ExecuteAsync(Guid currentUserId, CancellationToken ct)
        {
            List<Account> accounts = await unitOfWork.Account.GetItemsByPredicateAsync(
                account => !account.IsArchived &&
                           (account.CreatedByUserId == currentUserId || account.Members.Any(member => member.UserId == currentUserId)),
                asNoTracking: true,
                include: query => query
                    .Include(account => account.CreatedByUser)
                    .Include(account => account.Members),
                ct: ct);

            List<UserAccountViewModel> result = new();

            foreach (Account account in accounts.OrderBy(account => account.Name))
            {
                AccountMember? membership = account.Members.FirstOrDefault(member => member.UserId == currentUserId);

                result.Add(new UserAccountViewModel
                {
                    Id = account.Id,
                    Name = account.Name,
                    Description = account.Description,
                    ColorHex = account.ColorHex,
                    IsActive = !account.IsArchived,
                    CanManage = account.CreatedByUserId == currentUserId,
                    OwnerName = account.CreatedByUser?.Name ?? string.Empty,
                    AccessRole = membership?.Role ?? AccountAccessRole.Owner
                });
            }

            return result;
        }
    }
}
