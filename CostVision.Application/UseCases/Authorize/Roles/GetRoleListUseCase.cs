using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Domain.Models.Authorization;
using CostVision.Application.Models.Responses.Results;

namespace CostVision.Application.UseCases.Authorize.Roles
{
    public class GetRoleListUseCase(IUnitOfWork unitOfWork) : IGetRoleListUseCase
    {
        public async Task<ServiceResult<List<Role>>> ExecuteAsync(CancellationToken ct)
        {
            List<Role> roles = await unitOfWork.Role.GetItemsByPredicateAsync(asNoTracking: true, ct: ct);
            roles = roles.OrderBy(role => role.Name).ToList();

            return ServiceResult<List<Role>>.Ok(roles);
        }
    }
}
