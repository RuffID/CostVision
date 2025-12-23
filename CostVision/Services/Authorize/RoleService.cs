using CostVision.Interfaces.DataBase.Repositories;
using CostVision.Interfaces.Service.Authorize;
using CostVision.Models.Authorization;
using CostVision.Models.Responses.Results;

namespace CostVision.Services.Authorize
{
    public class RoleService(IUnitOfWork unitOfWork) : IRoleService
    {
        public async Task<ServiceResult<List<Role>>> GetRoleListAsync(CancellationToken ct)
        {
            List<Role> roles = await unitOfWork.Role.GetItemsByPredicate(asNoTracking: true, ct: ct);
            roles = roles.OrderBy(x => x.Name).ToList();
            return ServiceResult<List<Role>>.Ok(roles);
        }
    }
}
