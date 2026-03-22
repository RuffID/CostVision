using CostVision.Abstractions.DataBase.Repositories;
using CostVision.Models.Authorization;
using CostVision.Models.Enums.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CostVision.Services.Authorize.Attributes
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
            User? user = await unitOfWork.User.GetItemById(userId, true,
                include: u => u
                    .Include(u => u.Roles)
                    .Include(u => u.Accounts.Where(a => !a.IsArchived))
                    .AsSplitQuery(),
                ct: context.HttpContext.RequestAborted);

            if (user == null || !user.IsActive)
            {
                httpContext.Response.Redirect("login");
                return;
            }

            httpContext.Items[CURRENT_USER_ITEM_KEY] = user;

            if (!TryEnforcePageAccess(httpContext, user))
                return;

            await next();
        }

        public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

        private static bool TryEnforcePageAccess(HttpContext httpContext, User user)
        {
            string? path = httpContext.Request.Path.Value;

            if (string.IsNullOrWhiteSpace(path))
            {
                return true;
            }

            // Правило: страница Users доступна только админам
            if (IsUsersSettingsPage(path) && !HasAdminRole(user))
            {
                RedirectBackOrHome(httpContext);
                return false;
            }

            return true;
        }

        private static bool IsUsersSettingsPage(string path)
        {
            return string.Equals(path, "/Settings/Users", StringComparison.OrdinalIgnoreCase)
                || string.Equals(path, "/Settings/Users/", StringComparison.OrdinalIgnoreCase);

        }

        private static bool HasAdminRole(User user)
        {
            if (user.UserRoles != null)
            {
                foreach (UserRole link in user.UserRoles)
                {
                    Role? role = link.Role;
                    if (role != null && role.RoleType == RoleType.Admin)
                    {
                        return true;
                    }
                }
            }

            if (user.Roles != null)
            {
                foreach (Role role in user.Roles)
                {
                    if (role.RoleType == RoleType.Admin)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static void RedirectBackOrHome(HttpContext httpContext)
        {
            // Делать редирект назад, иначе на главную (или другую безопасную страницу)
            string? referer = httpContext.Request.Headers.Referer.FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(referer) && Uri.TryCreate(referer, UriKind.Absolute, out Uri? uri))
            {
                httpContext.Response.Redirect(uri.PathAndQuery);
                return;
            }

            httpContext.Response.Redirect("/");
        }
    }
}
