using CostVision.Domain.Models.Authorization;
using CostVision.Application.Models.Requests.Authorize;
using CostVision.Application.Models.Responses.Results;

namespace CostVision.Application.UseCases.Authorize.Authentication
{
    public interface IAuthenticateUserUseCase
    {
        Task<ServiceResult<User>> ExecuteAsync(LoginRequest request, CancellationToken ct);
    }
}
