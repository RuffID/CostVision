using CostVision.Models.Authorization;
using CostVision.Models.Responses.Results;

namespace CostVision.Interfaces.Service.Authorize
{
    public interface IRoleService
    {
        Task<ServiceResult<List<Role>>> GetRoleListAsync(CancellationToken ct);
    }
}
