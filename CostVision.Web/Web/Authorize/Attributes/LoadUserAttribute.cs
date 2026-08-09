using CostVision.Domain.Models.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CostVision.Web.Abstractions.Entity;

namespace CostVision.Web.Authorize.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class LoadUserAttribute : Attribute, IAsyncPageFilter, IOrderedFilter
    {
        public int Order { get; set; } = 1;
        public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context)
        {
            return Task.CompletedTask;
        }

        public Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
        {
            PageModel? page = context.HandlerInstance as PageModel;
            if (page is IHasCurrentUser target)
            {
                if (context.HttpContext.Items[CurrentUserHttpContextItemKeys.CURRENT_USER] is User user)
                {
                    target.CurrentUser = user;
                }
            }

            return next();
        }
    }
}
