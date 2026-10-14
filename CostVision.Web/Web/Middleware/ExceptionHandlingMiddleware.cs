using CostVision.Web.Extensions;

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
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                logger.LogWarning(
                    "Request cancelled. Path={Path}, TraceId={TraceId}",
                    context.Request.Path,
                    context.TraceIdentifier);
                if (!context.Response.HasStarted)
                {
                    context.Response.Clear();
                    context.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
                }
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Unhandled exception. Path={Path}, TraceId={TraceId}",
                    context.Request.Path,
                    context.TraceIdentifier);

                if (context.Response.HasStarted)
                {
                    throw;
                }

                context.Response.Clear();
                if (context.RequestAborted.IsCancellationRequested)
                {
                    context.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
                    return;
                }

                int status = StatusCodes.Status500InternalServerError;
                string message = "Внутренняя ошибка сервера.";
                if (exception is TimeoutException)
                {
                    status = StatusCodes.Status504GatewayTimeout;
                    message = "Сервер не успел выполнить запрос. Повторите попытку позже.";
                }
                else if (exception is BadHttpRequestException badRequest)
                {
                    status = badRequest.StatusCode;
                    message = "Некорректный HTTP-запрос или превышен допустимый размер запроса.";
                }

                context.Response.StatusCode = status;
                bool expectsHtml = context.Request.Headers.Accept.ToString().Contains("text/html", StringComparison.OrdinalIgnoreCase);
                bool expectsJson = CookieHttpContract.IsJsonRequest(context.Request) || !expectsHtml;
                if (expectsJson)
                {
                    await context.Response.WriteAsJsonAsync(new { success = false, message }, context.RequestAborted);
                }
                else
                {
                    context.Response.ContentType = "text/html; charset=utf-8";
                    await context.Response.WriteAsync("<!doctype html><html lang=\"ru\"><meta charset=\"utf-8\"><title>Ошибка</title><p>" + message + "</p></html>", context.RequestAborted);
                }
            }
        }
    }
}
