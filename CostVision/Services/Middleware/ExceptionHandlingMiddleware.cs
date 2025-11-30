namespace CostVision.Services.Middleware
{
    public sealed class ExceptionHandlingMiddleware(ILoggerFactory logger) : IMiddleware
    {
        private readonly ILogger<ExceptionHandlingMiddleware> _logger = logger.CreateLogger<ExceptionHandlingMiddleware>();

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            try
            {
                await next(context);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Request cancelled. Path={Path}, TraceId={TraceId}",
                    context.Request.Path, context.TraceIdentifier);
                context.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception. Path={Path}, TraceId={TraceId}",
                    context.Request.Path, context.TraceIdentifier);

                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsJsonAsync(new { error = "Internal server error", traceId = context.TraceIdentifier });
            }
        }
    }
}
