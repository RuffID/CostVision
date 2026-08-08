using CostVision.Application.Models.Dtos.Authorization;
using CostVision.Application.Models.Responses.Results;

namespace CostVision.Application.UseCases.Authorize.Roles
{
    public interface IGetRoleListUseCase
    {
        Task<ServiceResult<List<RoleDto>>> ExecuteAsync(CancellationToken ct);
    }
}
