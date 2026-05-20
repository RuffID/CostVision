using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Domain.Models.Authorization;
using CostVision.Application.Models.Dtos.Authorization;
using CostVision.Application.Models.Responses.Results;

namespace CostVision.Application.UseCases.Authorize.Users
{
    public class GetUserListUseCase(IUnitOfWork unitOfWork) : IGetUserListUseCase
    {
        public async Task<ServiceResult<List<UserListItemDto>>> ExecuteAsync(bool includeInactive, CancellationToken ct)
        {
            List<User> users = await unitOfWork.User.GetListWithRolesAsync(includeInactive, ct);

            List<UserListItemDto> items = users
                .OrderBy(user => user.Name)
                .Select(user => new UserListItemDto
                {
                    Id = user.Id,
                    Name = user.Name,
                    Login = user.Login,
                    IsActive = user.IsActive,
                    CreatedAtUtc = user.CreatedAtUtc,
                    LastLoginAtUtc = user.LastLoginAtUtc
                })
                .ToList();

            return ServiceResult<List<UserListItemDto>>.Ok(items);
        }
    }
}
