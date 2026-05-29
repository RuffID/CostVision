namespace CostVision.Infrastructure.IntegrationTests.Web.Models;

public sealed record AuthenticatedContext(string CookieHeader, string AntiforgeryToken);
