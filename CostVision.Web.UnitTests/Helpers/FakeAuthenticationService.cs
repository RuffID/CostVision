using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace CostVision.Web.UnitTests.Helpers;

internal sealed class FakeAuthenticationService : IAuthenticationService
{
    public ClaimsPrincipal? SignedInPrincipal { get; private set; }

    public AuthenticationProperties? SignedInProperties { get; private set; }

    public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
    {
        return Task.FromResult(AuthenticateResult.NoResult());
    }

    public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
    {
        return Task.CompletedTask;
    }

    public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
    {
        return Task.CompletedTask;
    }

    public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties)
    {
        SignedInPrincipal = principal;
        SignedInProperties = properties;
        return Task.CompletedTask;
    }

    public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
    {
        return Task.CompletedTask;
    }
}
