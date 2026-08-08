using CostVision.Application.Models.Dtos.Authorization;
using CostVision.Application.Models.Requests.Authorize;
using CostVision.Application.Models.Responses.Results;

namespace CostVision.Application.UseCases.Authorize.Authentication
{
    public interface IAuthenticateUserUseCase
    {
        Task<ServiceResult<AuthenticatedUserDto>> ExecuteAsync(LoginRequest request, CancellationToken ct);
    }
}
