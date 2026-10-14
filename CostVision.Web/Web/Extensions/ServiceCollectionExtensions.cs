using CostVision.Application.Extensions;
using CostVision.Infrastructure.Extensions;
using CostVision.Web.Authorize;
using CostVision.Web.Middleware;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;

namespace CostVision.Web.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection ConfigureServices(this IServiceCollection services, WebApplicationBuilder builder)
        {
            services.AddForwardedHeadersConfiguration(builder.Configuration);
            services.AddWebRuntimeConfiguration(builder.Configuration, builder.Environment.ContentRootPath);
            services.AddApplication();
            services.AddInfrastructure(builder.Configuration);
            services.AddTransient<ExceptionHandlingMiddleware>();
            services.AddControllers();
            services.AddLogging();
            services.AddHttpContextAccessor();
            services.AddAuthorization(options =>
            {
                options.AddPolicy(AuthorizationPolicies.ADMIN, policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.Requirements.Add(new AdminAccessRequirement());
                });
            });
            services.AddScoped<IAuthorizationHandler, AdminAccessHandler>();

            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "CostVision API",
                    Version = "v1"
                });
            });

            services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(CookieHttpContract.Configure);

            services.AddRazorPages(options =>
            {
                // Делает все ссылки на страницы с маленькой буквы
                options.Conventions.AddFolderRouteModelConvention("/", model =>
                {
                    foreach (var selector in model.Selectors)
                    {
                        var attrRoute = selector.AttributeRouteModel;
                        if (attrRoute?.Template != null)
                        {
                            attrRoute.Template = attrRoute.Template.ToLowerInvariant();
                        }
                    }
                });
            });

            return services;
        }
    }
}
