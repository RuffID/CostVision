using CostVision.Interfaces.DataBase.Repositories;
using CostVision.Models.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

namespace CostVision.Services.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class CookieAuthorizeAttribute(string cookieName = ".CostVision.Cookies") : Attribute, IAsyncPageFilter, IOrderedFilter
    {
        private const string CURRENT_USER_ITEM_KEY = "CurrentUser";
        public int Order { get; set; } = 0;
        public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
        {
            if (context.HandlerInstance is not PageModel page)
                throw new InvalidOperationException($"{nameof(CookieAuthorizeAttribute)} can only be used on Razor Page models.");

            HttpContext httpContext = context.HttpContext;
            HttpRequest request = httpContext.Request;

            if (!request.Cookies.TryGetValue(cookieName, out var cookieValue) || string.IsNullOrWhiteSpace(cookieValue))
            {
                httpContext.Response.Redirect("login");
                return;
            }

            string? userIdStr = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!Guid.TryParse(userIdStr, out var userId))
            {
                httpContext.Response.Redirect("login");
                return;
            }

            IUnitOfWork unitOfWork = httpContext.RequestServices.GetRequiredService<IUnitOfWork>();
            User? user = await unitOfWork.User.GetItemById(userId, true, context.HttpContext.RequestAborted, includes: u => u.Accounts.Where(a => !a.IsArchived));

            if (user == null || !user.IsActive)
            {
                httpContext.Response.Redirect("login");
                return;
            }

            httpContext.Items[CURRENT_USER_ITEM_KEY] = user;

            await next();
        }

        public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;
    }
}
