using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Extensions;
using System.Linq.Expressions;
using CostVision.Domain.Models.Enums.Authorization;
using CostVision.Domain.Models.Receipts;
using Microsoft.EntityFrameworkCore;

namespace CostVision.Application.UseCases.MoneyMovements
{
    public class GetMoneyMovementAccountsUseCase(IAccountRepository accountRepository) : IGetMoneyMovementAccountsUseCase
    {
        public async Task<List<UserAccountViewModel>> ExecuteAsync(Guid currentUserId, CancellationToken ct)
        {
            Expression<Func<Account, bool>> predicate = account => !account.IsArchived;
            List<Account> accounts = await accountRepository.GetItemsByPredicateAsync(
                predicate.WithAccountReadAccess(account => account, currentUserId),
                asNoTracking: true,
                include: query => query
                    .Include(account => account.CreatedByUser)
                    .Include(account => account.Members),
                ct: ct);

            List<UserAccountViewModel> result = new();

            foreach (Account account in accounts.OrderBy(account => account.Name))
            {
                AccountAccessRole role = FinancialDataAccess.GetRequiredAccountRole(account, currentUserId);

                result.Add(new UserAccountViewModel
                {
                    Id = account.Id,
                    Name = account.Name,
                    Description = account.Description,
                    ColorHex = account.ColorHex,
                    IsActive = !account.IsArchived,
                    CanManage = account.CreatedByUserId == currentUserId,
                    OwnerName = account.CreatedByUser?.Name ?? string.Empty,
                    AccessRole = role
                });
            }

            return result;
        }
    }
}
