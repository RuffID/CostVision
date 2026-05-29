using CostVision.Application.Models.Requests.Authorize;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.Authorize.Authentication;
using CostVision.Domain.Models.Authorization;
using Microsoft.AspNetCore.Http;

namespace CostVision.Infrastructure.IntegrationTests.Web.Fakes;

public sealed class FakeAuthenticateUserUseCase(User activeUser) : IAuthenticateUserUseCase
{
    public Task<ServiceResult<User>> ExecuteAsync(LoginRequest request, CancellationToken ct)
    {
        ServiceResult<User> result = request.Login switch
        {
            "active" when request.Password == "password" => ServiceResult<User>.Ok(activeUser),
            "blocked" => ServiceResult<User>.Fail(StatusCodes.Status403Forbidden, "User is blocked."),
            _ => ServiceResult<User>.Fail(StatusCodes.Status401Unauthorized, "Invalid credentials.")
        };

        return Task.FromResult(result);
    }
}
