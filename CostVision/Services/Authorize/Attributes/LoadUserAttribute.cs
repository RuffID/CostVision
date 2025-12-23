using CostVision.Interfaces.Entity;
using CostVision.Models.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CostVision.Services.Authorize.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class LoadUserAttribute : Attribute, IAsyncPageFilter, IOrderedFilter
    {
        public int Order { get; set; } = 1;
        private const string CURRENT_USER_ITEM_KEY = "CurrentUser";

        public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context)
        {
            return Task.CompletedTask;
        }

        public Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
        {
            PageModel? page = context.HandlerInstance as PageModel;
            if (page is IHasCurrentUser target)
            {
                if (context.HttpContext.Items[CURRENT_USER_ITEM_KEY] is User user)
                {
                    target.CurrentUser = user;
                }
            }

            return next();
        }
    }
}
