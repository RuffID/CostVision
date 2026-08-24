namespace CostVision.Infrastructure.IntegrationTests.Web.Models;

public sealed record AntiforgeryContext(string CookieHeader, string Token);
