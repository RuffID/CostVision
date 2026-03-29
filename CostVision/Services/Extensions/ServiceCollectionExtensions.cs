using CostVision.Abstractions.Api;
using CostVision.Abstractions.DataBase.Repositories;
using CostVision.Abstractions.DataBase.Repositories.Authorization;
using CostVision.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Abstractions.Service.Authorize;
using CostVision.Abstractions.Service.Receipts;
using CostVision.DataBase;
using CostVision.DataBase.Repositories;
using CostVision.DataBase.Repositories.Authorization;
using CostVision.DataBase.Repositories.Receipts;
using CostVision.Models.ConfigClass;
using CostVision.Services.Api;
using CostVision.Services.Authorize;
using CostVision.Services.BackgroundServices;
using CostVision.Services.DataBase;
using CostVision.Services.Helpers;
using CostVision.Services.Middleware;
using CostVision.Services.Receipts;
using EFCoreLibrary.Abstractions.Database;
using EFCoreLibrary.EfCore;
using EFCoreLibrary.Extensions;
using HttpClientLibrary;
using HttpClientLibrary.Abstractions;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Newtonsoft.Json;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;

namespace CostVision.Services.Extensions
{
    public static class ServiceCollectionExtensions
    {
        private static IServiceCollection AddConfig(this IServiceCollection services, IConfiguration conf)
        {
            services.Configure<ApiEndpointOptions>(conf.GetSection(ApiEndpointOptions.SectionName));
            services.Configure<ProverkachekaOptions>(opt => { opt.ProverkachekaApiToken = conf[ProverkachekaOptions.SectionName]!; });

            return services;
        }

        public static IServiceCollection ConfigureServices(this IServiceCollection services, WebApplicationBuilder builder)
        {
            services.AddConfig(builder.Configuration);
            services.AddControllers();
            services.AddLogging();
            services.AddAuthorization();

            services.AddTransient<ExceptionHandlingMiddleware>();

            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "Моё API",
                    Version = "v1"
                });
            });

            services.AddDbContext<ApplicationContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("MSSql")));
            services.AddScoped<IAppDbContext<ApplicationContext>>(sp => new EfDbContextAdapter<ApplicationContext>(sp.GetRequiredService<ApplicationContext>()));
            services.AddEfCoreBaseRepositories<ApplicationContext>();

            services.AddHttpClient<IHttpApiClient, HttpApiClient>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(180);
            });

            services.AddScoped<DataBaseCheckUpService<ApplicationContext>>();
            services.AddScoped(sp =>
            {
                ILoggerFactory loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                string connectionString = builder.Configuration.GetConnectionString("MSSql")!;
                string backupFolder = OperatingSystem.IsLinux() ? "/var/opt/mssql/backups" : Path.Combine(AppContext.BaseDirectory, "Backups");
                return new BackupService<ApplicationContext>(connectionString, backupFolder, loggerFactory);
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

            services.AddScoped<Hasher>();
            services.AddScoped<QrParser>();
            services.AddScoped<IReceiptAccessVerificationService, ReceiptAccessVerificationService>();
            services.AddScoped<IReceiptRequest, ReceiptRequest>();
            services.AddScoped<IReceiptService, ReceiptService>();
            services.AddScoped<IAccountService, AccountService>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IRoleService, RoleService>();

            services.AddHostedService<ReceiptRefreshBackgroundService>();

            ConfigureRepositoryServices(services);

            return services;
        }

        private static void ConfigureRepositoryServices(IServiceCollection services)
        {
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IRoleRepository, RoleRepository>();
            services.AddScoped<IAccountRepository, AccountRepository>();
            services.AddScoped<IAccountMemberRepository, AccountMemberRepository>();
            services.AddScoped<IReceiptAccountRepository, ReceiptAccountRepository>();
            services.AddScoped<IReceiptRepository, ReceiptRepository>();
            services.AddScoped<IReceiptItemRepository, ReceiptItemRepository>();
            services.AddScoped<IProductRepository, ProductRepository>();
        }
    }
}
