using CostVision.Application.Models.Dtos.Authorization;
using CostVision.Application.Models.Requests.Authorize;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.Authorize.Authentication;
using CostVision.Domain.Models.Authorization;

namespace CostVision.Infrastructure.IntegrationTests.Web.Fakes;

public sealed class FakeAuthenticateUserUseCase(User activeUser) : IAuthenticateUserUseCase
{
    public Task<ServiceResult<AuthenticatedUserDto>> ExecuteAsync(LoginRequest request, CancellationToken ct)
    {
        AuthenticatedUserDto user = new()
        {
            Id = activeUser.Id,
            Name = activeUser.Name,
            Roles = activeUser.Roles.Select(role => role.RoleType).ToList()
        };
        ServiceResult<AuthenticatedUserDto> result = request.Login switch
        {
            "active" when request.Password == "password" => ServiceResult<AuthenticatedUserDto>.Ok(user),
            "blocked" => ServiceResult<AuthenticatedUserDto>.Fail(ServiceErrorType.Forbidden, "User is blocked."),
            _ => ServiceResult<AuthenticatedUserDto>.Fail(ServiceErrorType.Unauthorized, "Invalid credentials.")
        };

        return Task.FromResult(result);
    }
}
