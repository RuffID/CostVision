using CostVision.Application.UseCases.Authorize.Authentication;
using CostVision.Domain.Models.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

namespace CostVision.Web.Authorize.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class CookieAuthorizeAttribute : Attribute, IAsyncPageFilter, IOrderedFilter
    {
        public int Order { get; set; } = 0;

        public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
        {
            if (context.HandlerInstance is not PageModel)
                throw new InvalidOperationException($"{nameof(CookieAuthorizeAttribute)} can only be used on Razor Page models.");

            HttpContext httpContext = context.HttpContext;

            if (httpContext.User.Identity?.IsAuthenticated != true)
            {
                context.Result = new ChallengeResult(CookieAuthenticationDefaults.AuthenticationScheme);
                return;
            }

            string? userIdStr = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!Guid.TryParse(userIdStr, out var userId))
            {
                context.Result = new ChallengeResult(CookieAuthenticationDefaults.AuthenticationScheme);
                return;
            }

            IGetActiveUserForRequestUseCase getActiveUserForRequestUseCase = httpContext.RequestServices
                .GetRequiredService<IGetActiveUserForRequestUseCase>();
            User? user = await getActiveUserForRequestUseCase.ExecuteAsync(userId, httpContext.RequestAborted);

            if (user == null)
            {
                await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                context.Result = new ChallengeResult(CookieAuthenticationDefaults.AuthenticationScheme);
                return;
            }

            httpContext.Items[CurrentUserHttpContextItemKeys.CURRENT_USER] = user;

            await next();
        }

        public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;
    }
}
