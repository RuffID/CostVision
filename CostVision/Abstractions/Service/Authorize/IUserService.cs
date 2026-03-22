using CostVision.Models.Dtos.Authorization;
using CostVision.Models.Requests.Authorize;
using CostVision.Models.Responses.Results;

namespace CostVision.Abstractions.Service.Authorize
{
    public interface IUserService
    {
        Task<ServiceResult<List<UserListItemDto>>> GetUserListAsync(bool includeInactive, CancellationToken ct);
        Task<ServiceResult<UserEditDto>> GetUserAsync(Guid id, CancellationToken ct);

        Task<ServiceResult> CreateUserAsync(UserUpsertRequest dto, CancellationToken ct);
        Task<ServiceResult> UpdateUserAsync(UserUpsertRequest dto, CancellationToken ct);

        Task<ServiceResult<bool>> ToggleUserActiveAsync(Guid id, CancellationToken ct);
    }
}
