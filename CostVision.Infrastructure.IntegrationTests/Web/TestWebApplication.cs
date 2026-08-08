using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.RegularExpressions;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Authorization;
using CostVision.Application.Models.Dtos.Dashboard;
using CostVision.Application.Models.Requests.Dashboard;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.Authorize.Authentication;
using CostVision.Application.UseCases.Authorize.Users;
using CostVision.Application.UseCases.Dashboard;
using CostVision.Application.UseCases.Receipts.Accounts;
using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.Enums.Authorization;
using CostVision.Infrastructure.IntegrationTests.Web.Fakes;
using CostVision.Infrastructure.IntegrationTests.Web.Helpers;
using CostVision.Infrastructure.IntegrationTests.Web.Models;
using CostVision.Web.Pages;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace CostVision.Infrastructure.IntegrationTests.Web;

public sealed class TestWebApplication : IDisposable
{
    private readonly FakeAuthenticateUserUseCase _authenticateUser;
    private readonly IHost _host;
    private readonly string _keyDirectory = Path.Combine(Path.GetTempPath(), "CostVisionWebIntegrationTests", Guid.NewGuid().ToString("N"));

    public TestWebApplication()
    {
        ActiveUser = CreateActiveUser();

        _authenticateUser = new FakeAuthenticateUserUseCase(ActiveUser);
        MarkUserActivity = new FakeMarkUserActivityUseCase();

        _host = new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.UseContentRoot(GetWebProjectPath());
                webBuilder.ConfigureServices(ConfigureServices);
                webBuilder.Configure(ConfigurePipeline);
            })
            .Start();

        Client = _host.GetTestClient();
        Client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public HttpClient Client { get; }

    public User ActiveUser { get; }

    public FakeMarkUserActivityUseCase MarkUserActivity { get; }

    public async Task<AuthenticatedContext> LoginAsync()
    {
        AntiforgeryContext antiforgery = await GetAntiforgeryContextAsync();
        HttpResponseMessage response = await PostLoginAsync(antiforgery, "active", "password");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        string cookieHeader = CookieHeader.Merge(antiforgery.CookieHeader, response);
        AntiforgeryContext authenticatedAntiforgery = await GetAntiforgeryContextAsync("/", cookieHeader);

        return new AuthenticatedContext(authenticatedAntiforgery.CookieHeader, authenticatedAntiforgery.Token);
    }

    public Task<AntiforgeryContext> GetAntiforgeryContextAsync()
    {
        return GetAntiforgeryContextAsync("/login", null);
    }

    private async Task<AntiforgeryContext> GetAntiforgeryContextAsync(string path, string? cookieHeader)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpRequestMessage request = new(HttpMethod.Get, path);

        if (!string.IsNullOrWhiteSpace(cookieHeader))
            request.Headers.Add("Cookie", cookieHeader);

        HttpResponseMessage response = await Client.SendAsync(request, ct);
        string html = await response.Content.ReadAsStringAsync(ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return new AntiforgeryContext(
            CookieHeader.Merge(cookieHeader, response),
            ParseRequestVerificationToken(html));
    }

    public async Task<HttpResponseMessage> PostLoginAsync(
        AntiforgeryContext antiforgery,
        string login,
        string password)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, "/login")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Input.Login"] = login,
                ["Input.Password"] = password,
                ["__RequestVerificationToken"] = antiforgery.Token
            })
        };

        request.Headers.Add("Cookie", antiforgery.CookieHeader);

        return await Client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    public async Task<HttpResponseMessage> PostUserActivityPingAsync(AuthenticatedContext context)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, "/user-activity?handler=Ping")
        {
            Content = new StringContent(string.Empty)
        };

        request.Headers.Add("Cookie", context.CookieHeader);
        request.Headers.Add("RequestVerificationToken", context.AntiforgeryToken);

        return await Client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    public void Dispose()
    {
        Client.Dispose();
        _host.Dispose();

        if (Directory.Exists(_keyDirectory))
            Directory.Delete(_keyDirectory, true);
    }

    private void ConfigureServices(IServiceCollection services)
    {
        Directory.CreateDirectory(_keyDirectory);

        services.AddLogging();
        services.AddRouting();
        services.AddAuthorization();
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = ".CostVision.Cookies";
                options.LoginPath = "/login";
                options.SlidingExpiration = true;
                options.ExpireTimeSpan = TimeSpan.FromDays(14);
            });

        services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(_keyDirectory))
            .SetApplicationName("CostVision.Web.IntegrationTests");

        services.AddSingleton<IAuthenticateUserUseCase>(_authenticateUser);
        services.AddSingleton<IMarkUserActivityUseCase>(MarkUserActivity);
        services.AddSingleton<IGetDashboardIncomeExpenseReportUseCase, FakeDashboardIncomeExpenseReportUseCase>();
        services.AddSingleton<IGetUserAccountsUseCase, FakeGetUserAccountsUseCase>();
        services.AddSingleton(CreateUnitOfWork());

        services.AddRazorPages()
            .AddApplicationPart(typeof(LoginModel).Assembly);
    }

    private void ConfigurePipeline(IApplicationBuilder app)
    {
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapRazorPages();
            endpoints.MapGet("/test-auth", WriteAuthenticationProbeAsync);
        });
    }

    private Task WriteAuthenticationProbeAsync(HttpContext context)
    {
        return context.Response.WriteAsJsonAsync(new
        {
            authenticated = context.User.Identity?.IsAuthenticated == true,
            nameIdentifier = context.User.FindFirstValue(ClaimTypes.NameIdentifier),
            name = context.User.FindFirstValue(ClaimTypes.Name),
            roles = context.User.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray()
        });
    }

    private IUnitOfWork CreateUnitOfWork()
    {
        Mock<IUserRepository> userRepository = new();
        userRepository
            .Setup(repository => repository.GetItemByIdAsync(
                It.IsAny<Guid>(),
                It.IsAny<bool>(),
                It.IsAny<Func<IQueryable<User>, IQueryable<User>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, bool _, Func<IQueryable<User>, IQueryable<User>>? _, CancellationToken _) =>
                id == ActiveUser.Id ? ActiveUser : null);

        Mock<IUnitOfWork> unitOfWork = new();
        unitOfWork.SetupGet(current => current.User).Returns(userRepository.Object);

        return unitOfWork.Object;
    }

    private static string GetWebProjectPath()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null)
        {
            string projectPath = Path.Combine(directory.FullName, "CostVision.Web");
            if (File.Exists(Path.Combine(projectPath, "CostVision.Web.csproj")))
                return projectPath;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Не удалось найти каталог проекта CostVision.Web.");
    }

    private static User CreateActiveUser()
    {
        bool isRoleCreated = Role.TryCreate("Admin", RoleType.Admin, out Role? role, out string? error);
        if (!isRoleCreated || role == null)
            throw new InvalidOperationException(error ?? "Не удалось создать тестовую роль.");

        role.Id = Guid.Parse("22222222-2222-2222-2222-222222222222");

        bool isUserCreated = User.TryCreate(
            "active",
            "Integration User",
            "password-hash",
            [role],
            new DateTime(2026, 1, 1),
            out User? user,
            out error);
        if (!isUserCreated || user == null)
            throw new InvalidOperationException(error ?? "Не удалось создать тестового пользователя.");

        user.Id = Guid.Parse("11111111-1111-1111-1111-111111111111");
        return user;
    }

    private static string ParseRequestVerificationToken(string html)
    {
        System.Text.RegularExpressions.Match metaTokenMatch = Regex.Match(
            html,
            "<meta name=\"request-verification-token\" content=\"(?<token>[^\"]+)\"");

        if (metaTokenMatch.Success)
            return WebUtility.HtmlDecode(metaTokenMatch.Groups["token"].Value);

        System.Text.RegularExpressions.Match inputTokenMatch = Regex.Match(
            html,
            "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"(?<token>[^\"]+)\"");

        Assert.True(inputTokenMatch.Success);

        return WebUtility.HtmlDecode(inputTokenMatch.Groups["token"].Value);
    }

    private sealed class FakeDashboardIncomeExpenseReportUseCase : IGetDashboardIncomeExpenseReportUseCase
    {
        public Task<ServiceResult<DashboardIncomeExpenseReportDto>> ExecuteAsync(User currentUser, DashboardIncomeExpenseReportRequest request, CancellationToken ct)
        {
            return Task.FromResult(ServiceResult<DashboardIncomeExpenseReportDto>.Ok(new DashboardIncomeExpenseReportDto()));
        }
    }

    private sealed class FakeGetUserAccountsUseCase : IGetUserAccountsUseCase
    {
        public Task<ServiceResult<List<UserAccountViewModel>>> ExecuteAsync(Guid userId, bool includeArchived, CancellationToken ct)
        {
            return Task.FromResult(ServiceResult<List<UserAccountViewModel>>.Ok(new List<UserAccountViewModel>()));
        }
    }
}
