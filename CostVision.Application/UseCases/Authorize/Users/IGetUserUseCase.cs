using CostVision.Application.Models.Dtos.Authorization;
using CostVision.Application.Models.Responses.Results;

namespace CostVision.Application.UseCases.Authorize.Users
{
    public interface IGetUserUseCase
    {
        Task<ServiceResult<UserEditDto>> ExecuteAsync(Guid id, CancellationToken ct);
    }
}
