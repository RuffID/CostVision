using CostVision.Domain.Models.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CostVision.Web.Extensions
{
    public static class PageModelUserExtensions
    {
        public static User GetCurrentUser(this PageModel page)
        {
            object? value = page.HttpContext.Items["CurrentUser"];

            if (value is User user)
                return user;

            throw new InvalidOperationException("CurrentUser was not set in HttpContext.Items.");
        }
    }
}
