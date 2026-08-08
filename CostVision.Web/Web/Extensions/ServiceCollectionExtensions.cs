using CostVision.Application.Extensions;
using CostVision.Infrastructure.Extensions;
using CostVision.Web.Middleware;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.OpenApi;
using System.Reflection;
using System.Runtime.InteropServices;

namespace CostVision.Web.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection ConfigureServices(this IServiceCollection services, WebApplicationBuilder builder)
        {
            services.AddApplication();
            services.AddInfrastructure(builder.Configuration);
            services.AddTransient<ExceptionHandlingMiddleware>();
            services.AddControllers();
            services.AddLogging();
            services.AddAuthorization();

            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "Моё API",
                    Version = "v1"
                });
            });

            services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {
                    options.Cookie.Name = ".CostVision.Cookies";
                    options.LoginPath = "/login";
                    // Куки будут автоматически продливаться
                    options.SlidingExpiration = true;
                    // Если пользователь не будет заходить 14 дней подряд, то куки пропадут
                    options.ExpireTimeSpan = TimeSpan.FromDays(14);
                });

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


            // Определяет путь в зависимости от ОС для папки, где будут храниться ключи для Data Protection
            string keyPath;
            string projectName = Assembly.GetEntryAssembly()?.GetName().Name ?? "DefaultAppName";

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                keyPath = Path.Combine(builder.Environment.ContentRootPath, "keys-windows");
                // Убедиться, что папка существует
                Directory.CreateDirectory(keyPath);
            }
            else
                keyPath = Path.Combine(builder.Environment.ContentRootPath, "keys-linux");

            // Настроить Data Protection
            services.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(keyPath))
                .SetApplicationName(projectName);

            return services;
        }
    }
}
