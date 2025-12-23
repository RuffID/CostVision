using CostVision.Models.Dtos.Authorization;
using CostVision.Models.PageModels;
using CostVision.Models.Responses.Results;

namespace CostVision.Interfaces.Service.Authorize
{
    public interface IUserService
    {
        Task<ServiceResult<List<UserListItemDto>>> GetUserListAsync(bool includeInactive, CancellationToken ct);
        Task<ServiceResult<UserEditDto>> GetUserAsync(Guid id, CancellationToken ct);

        Task<ServiceResult> CreateUserAsync(UserUpsertPageModel dto, CancellationToken ct);
        Task<ServiceResult> UpdateUserAsync(UserUpsertPageModel dto, CancellationToken ct);

        Task<ServiceResult<bool>> ToggleUserActiveAsync(Guid id, CancellationToken ct);
    }
}
