using CostVision.Models.Authorization;
using CostVision.Models.Responses.Results;

namespace CostVision.Abstractions.Service.Authorize
{
    public interface IRoleService
    {
        Task<ServiceResult<List<Role>>> GetRoleListAsync(CancellationToken ct);
    }
}
