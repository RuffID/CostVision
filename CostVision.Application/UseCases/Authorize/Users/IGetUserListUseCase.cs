using CostVision.Application.Models.Dtos.Authorization;
using CostVision.Application.Models.Responses.Results;

namespace CostVision.Application.UseCases.Authorize.Users
{
    public interface IGetUserListUseCase
    {
        Task<ServiceResult<List<UserListItemDto>>> ExecuteAsync(bool includeInactive, CancellationToken ct);
    }
}
