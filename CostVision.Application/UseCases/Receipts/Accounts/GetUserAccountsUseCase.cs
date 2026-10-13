using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Extensions;
using System.Linq.Expressions;
using CostVision.Application.Models.Responses.Results;
using CostVision.Domain.Models.Enums.Authorization;
using CostVision.Domain.Models.Receipts;
using Microsoft.EntityFrameworkCore;

namespace CostVision.Application.UseCases.Receipts.Accounts
{
    public class GetUserAccountsUseCase(IUnitOfWork unitOfWork) : IGetUserAccountsUseCase
    {
        public async Task<ServiceResult<List<UserAccountViewModel>>> ExecuteAsync(Guid userId, bool includeArchived, CancellationToken ct)
        {
            Expression<Func<Account, bool>> predicate = account => includeArchived || !account.IsArchived;
            List<Account> accounts = await unitOfWork.Account.GetItemsByPredicateAsync(
                predicate.WithAccountReadAccess(account => account, userId),
                asNoTracking: true,
                include: query => query.Include(account => account.CreatedByUser).Include(account => account.Members),
                ct: ct);

            List<UserAccountViewModel> result = accounts.Select(account => new UserAccountViewModel
            {
                Id = account.Id,
                Name = account.Name,
                Description = account.Description,
                ColorHex = account.ColorHex,
                IsActive = !account.IsArchived,
                CanManage = account.CreatedByUserId == userId,
                OwnerName = account.CreatedByUser!.Name,
                AccessRole = FinancialDataAccess.GetRequiredAccountRole(account, userId)
            }).ToList();

            return ServiceResult<List<UserAccountViewModel>>.Ok(result);
        }
    }
}
