using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Models.Dtos.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.Enums.Authorization;
using CostVision.Domain.Models.Receipts;

namespace CostVision.Application.UseCases.Receipts.Accounts
{
    public class GetAccountShareUsersUseCase(IUnitOfWork unitOfWork) : IGetAccountShareUsersUseCase
    {
        public async Task<ServiceResult<List<AccountShareUserDto>>> ExecuteAsync(Guid accountId, Guid ownerUserId, CancellationToken ct)
        {
            if (accountId == Guid.Empty)
                return ServiceResult<List<AccountShareUserDto>>.Fail(400, "Некорректный идентификатор счёта.");

            Account? account = await unitOfWork.Account.GetOwnedByIdWithMembersAsync(accountId, ownerUserId, asNoTracking: true, ct: ct);
            if (account == null)
                return ServiceResult<List<AccountShareUserDto>>.Fail(404, "Счёт не найден.");

            Dictionary<Guid, AccountAccessRole> selectedUsers = account.Members
                .Where(member => member.UserId != ownerUserId)
                .ToDictionary(member => member.UserId, member => member.Role);

            List<User> users = await unitOfWork.User.GetItemsByPredicateAsync(user => user.IsActive && user.Id != ownerUserId, asNoTracking: true, ct: ct);

            List<AccountShareUserDto> items = users
                .OrderBy(user => user.Name)
                .Select(user => new AccountShareUserDto
                {
                    Id = user.Id,
                    Name = user.Name,
                    Login = user.Login,
                    IsSelected = selectedUsers.ContainsKey(user.Id),
                    Role = selectedUsers.TryGetValue(user.Id, out AccountAccessRole role) ? role : null
                })
                .ToList();

            return ServiceResult<List<AccountShareUserDto>>.Ok(items);
        }
    }
}
