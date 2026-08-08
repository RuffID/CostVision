using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Models.Dtos.Authorization;
using CostVision.Domain.Models.Authorization;
using CostVision.Application.Models.Responses.Results;

namespace CostVision.Application.UseCases.Authorize.Roles
{
    public class GetRoleListUseCase(IUnitOfWork unitOfWork) : IGetRoleListUseCase
    {
        public async Task<ServiceResult<List<RoleDto>>> ExecuteAsync(CancellationToken ct)
        {
            List<Role> roles = await unitOfWork.Role.GetItemsByPredicateAsync(asNoTracking: true, ct: ct);
            roles = roles.OrderBy(role => role.Name).ToList();

            List<RoleDto> result = roles
                .Select(role => new RoleDto
                {
                    Id = role.Id,
                    Name = role.Name,
                    RoleType = role.RoleType
                })
                .ToList();

            return ServiceResult<List<RoleDto>>.Ok(result);
        }
    }
}
