using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace CostVision.Web.Extensions;

public static class CookieHttpContract
{
    public static bool IsJsonRequest(HttpRequest request)
    {
        bool acceptsJson = request.Headers.Accept.Any(value => value != null && value.Contains("application/json", StringComparison.OrdinalIgnoreCase));
        bool isAjax = string.Equals(request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

        return acceptsJson || isAjax || request.HasJsonContentType();
    }

    public static void Configure(CookieAuthenticationOptions options)
    {
        options.Cookie.Name = ".CostVision.Cookies";
        options.LoginPath = "/login";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.Events.OnRedirectToLogin = context => RedirectAsync(context, StatusCodes.Status401Unauthorized,
            "Сессия истекла. Войдите в систему снова.");
        options.Events.OnRedirectToAccessDenied = context => RedirectAsync(context, StatusCodes.Status403Forbidden,
            "Недостаточно прав для выполнения операции.");
    }

    private static Task RedirectAsync(RedirectContext<CookieAuthenticationOptions> context, int status, string message)
    {
        if (context.Response.HasStarted)
        {
            throw new InvalidOperationException("Нельзя изменить ответ авторизации после его начала.");
        }

        if (IsJsonRequest(context.Request))
        {
            context.Response.StatusCode = status;
            return context.Response.WriteAsJsonAsync(new { success = false, message }, context.HttpContext.RequestAborted);
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    }
}
