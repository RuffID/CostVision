using CostVision.Domain.Models.Authorization;
using CostVision.Application.Models.Responses.Results;

namespace CostVision.Application.UseCases.Authorize.Roles
{
    public interface IGetRoleListUseCase
    {
        Task<ServiceResult<List<Role>>> ExecuteAsync(CancellationToken ct);
    }
}
