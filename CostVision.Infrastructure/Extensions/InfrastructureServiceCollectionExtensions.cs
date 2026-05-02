using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Authorization;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.Abstractions.Service.Authorize;
using CostVision.Application.Abstractions.Service.Receipts;
using CostVision.Infrastructure.DataBase;
using CostVision.Infrastructure.DataBase.Repositories;
using CostVision.Infrastructure.DataBase.Repositories.Authorization;
using CostVision.Infrastructure.DataBase.Repositories.Receipts;
using CostVision.Infrastructure.Abstractions.Api;
using CostVision.Infrastructure.Models.ConfigClass;
using CostVision.Infrastructure.Services.Api;
using CostVision.Infrastructure.Services.BackgroundServices;
using CostVision.Infrastructure.Services.DataBase;
using CostVision.Infrastructure.Services.Helpers;
using CostVision.Infrastructure.Services.Middleware;
using CostVision.Infrastructure.Services.Receipts;
using EFCoreLibrary.Abstractions.Database;
using EFCoreLibrary.EfCore;
using EFCoreLibrary.Extensions;
using HttpClientLibrary;
using HttpClientLibrary.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CostVision.Infrastructure.Extensions
{
    public static class InfrastructureServiceCollectionExtensions
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddInfrastructureConfig(configuration);

            services.AddTransient<ExceptionHandlingMiddleware>();

            services.AddDbContext<ApplicationContext>(options => options.UseSqlServer(configuration.GetConnectionString("MSSql")));
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
                string connectionString = configuration.GetConnectionString("MSSql")!;
                string backupFolder = OperatingSystem.IsLinux() ? "/var/opt/mssql/backups" : Path.Combine(AppContext.BaseDirectory, "Backups");
                return new BackupService<ApplicationContext>(connectionString, backupFolder, loggerFactory);
            });

            services.AddScoped<Hasher>();
            services.AddScoped<IPasswordHasher>(sp => sp.GetRequiredService<Hasher>());
            services.AddScoped<IQrParser, QrParser>();
            services.AddScoped<IReceiptAccessVerificationService, ReceiptAccessVerificationService>();
            services.AddScoped<IReceiptRequest, ReceiptRequest>();
            services.AddScoped<IExternalReceiptProvider, ExternalReceiptProvider>();

            services.AddHostedService<ReceiptRefreshBackgroundService>();

            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IRoleRepository, RoleRepository>();
            services.AddScoped<IAccountRepository, AccountRepository>();
            services.AddScoped<IAccountMemberRepository, AccountMemberRepository>();
            services.AddScoped<IReceiptAccountRepository, ReceiptAccountRepository>();
            services.AddScoped<IReceiptRepository, ReceiptRepository>();
            services.AddScoped<IReceiptItemRepository, ReceiptItemRepository>();
            services.AddScoped<IProductRepository, ProductRepository>();

            return services;
        }

        private static IServiceCollection AddInfrastructureConfig(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<ApiEndpointOptions>(configuration.GetSection(ApiEndpointOptions.SectionName));
            services.Configure<ProverkachekaOptions>(options =>
            {
                options.ProverkachekaApiToken = configuration[ProverkachekaOptions.SectionName]!;
            });

            return services;
        }
    }
}
