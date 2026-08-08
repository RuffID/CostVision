namespace CostVision.Web.Middleware
{
    public class ExceptionHandlingMiddleware(ILogger<ExceptionHandlingMiddleware> logger) : IMiddleware
    {
        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            try
            {
                await next(context);
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning(
                    "Request cancelled. Path={Path}, TraceId={TraceId}",
                    context.Request.Path,
                    context.TraceIdentifier);
                context.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Unhandled exception. Path={Path}, TraceId={TraceId}",
                    context.Request.Path,
                    context.TraceIdentifier);

                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsJsonAsync(new
                {
                    success = false,
                    message = "Внутренняя ошибка сервера."
                });
            }
        }
    }
}
