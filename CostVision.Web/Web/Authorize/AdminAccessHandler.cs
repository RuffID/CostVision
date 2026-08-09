using CostVision.Application.UseCases.Authorize.Authentication;
using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.Enums.Authorization;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace CostVision.Web.Authorize
{
    /// <summary>
    /// Проверяет актуальную роль администратора у аутентифицированного пользователя.
    /// </summary>
    public class AdminAccessHandler(
        IGetActiveUserForRequestUseCase getActiveUserForRequestUseCase,
        IHttpContextAccessor httpContextAccessor)
        : AuthorizationHandler<AdminAccessRequirement>
    {
        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, AdminAccessRequirement requirement)
        {
            string? userIdValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdValue, out Guid userId))
                return;

            CancellationToken ct = httpContextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None;
            User? user = await getActiveUserForRequestUseCase.ExecuteAsync(userId, ct);
            if (user?.Roles.Any(role => role.RoleType == RoleType.Admin) == true)
                context.Succeed(requirement);
        }
    }
}
