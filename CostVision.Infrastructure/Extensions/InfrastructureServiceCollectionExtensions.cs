using CostVision.Application.Abstractions.DataBase;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Authorization;
using CostVision.Application.Abstractions.DataBase.Repositories.MoneyMovements;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.Abstractions.Service.Authorize;
using CostVision.Application.Abstractions.Service.MoneyMovements;
using CostVision.Application.Abstractions.Service.Receipts;
using CostVision.Infrastructure.DataBase;
using CostVision.Infrastructure.DataBase.Repositories;
using CostVision.Infrastructure.DataBase.Repositories.Authorization;
using CostVision.Infrastructure.DataBase.Repositories.MoneyMovements;
using CostVision.Infrastructure.DataBase.Repositories.Receipts;
using CostVision.Infrastructure.Abstractions.Api;
using CostVision.Infrastructure.Models.ConfigClass;
using CostVision.Infrastructure.Services.Api;
using CostVision.Infrastructure.Services.BackgroundServices;
using CostVision.Infrastructure.Services.DataBase;
using CostVision.Infrastructure.Services.Helpers;
using CostVision.Infrastructure.Services.MoneyMovements;
using CostVision.Infrastructure.Services.Receipts;
using EFCoreLibrary.Abstractions.Database;
using EFCoreLibrary.EfCore;
using EFCoreLibrary.Extensions;
using HttpClientLibrary.Abstractions;
using HttpClientLibrary.Clients;
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
            services.AddSingleton(TimeProvider.System);

            string connectionString = configuration.GetConnectionString("MSSql")
                ?? throw new InvalidOperationException("Не задана обязательная строка подключения ConnectionStrings:MSSql.");

            services.AddDbContext<ApplicationContext>(options => options.UseSqlServer(connectionString));
            services.AddScoped<IAppDbContext<ApplicationContext>>(sp => new EfDbContextAdapter<ApplicationContext>(sp.GetRequiredService<ApplicationContext>()));
            services.AddScoped<IAppDbContext<AppDbContextBase>>(sp => new EfDbContextAdapter<AppDbContextBase>(sp.GetRequiredService<ApplicationContext>()));
            services.AddEfCoreBaseRepositories<ApplicationContext>();

            services.AddHttpClient<IHttpApiClient, HttpApiClient>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(180);
            });

            services.AddScoped<DataBaseCheckUpService<ApplicationContext>>();
            services.AddSingleton<IBackupFilePathBuilder, BackupFilePathBuilder>();
            services.AddScoped(sp =>
            {
                ILoggerFactory loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                IBackupFilePathBuilder backupFilePathBuilder = sp.GetRequiredService<IBackupFilePathBuilder>();
                string backupFolder = OperatingSystem.IsLinux() ? "/var/opt/mssql/backups" : Path.Combine(AppContext.BaseDirectory, "Backups");
                return new BackupService<ApplicationContext>(connectionString, backupFolder, loggerFactory, backupFilePathBuilder);
            });
            services.AddScoped<IBackupService<ApplicationContext>>(sp => sp.GetRequiredService<BackupService<ApplicationContext>>());

            services.AddScoped<Hasher>();
            services.AddScoped<IPasswordHasher>(sp => sp.GetRequiredService<Hasher>());
            services.AddScoped<IQrParser, QrParser>();
            services.AddScoped<IReceiptAccessVerificationService, ReceiptAccessVerificationService>();
            services.AddScoped<IReceiptRequest, ReceiptRequest>();
            services.AddScoped<IExternalReceiptProvider, ExternalReceiptProvider>();
            services.AddScoped<IBankStatementPdfTextExtractor, BankStatementPdfTextExtractor>();

            services.AddSingleton<IReceiptRefreshBackgroundScheduler, MidnightReceiptRefreshBackgroundScheduler>();
            services.AddHostedService<ReceiptRefreshBackgroundService>();

            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IRoleRepository, RoleRepository>();
            services.AddScoped<IAccountRepository, AccountRepository>();
            services.AddScoped<IAccountMemberRepository, AccountMemberRepository>();
            services.AddScoped<IReceiptAccountRepository, ReceiptAccountRepository>();
            services.AddScoped<IReceiptRepository, ReceiptRepository>();
            services.AddScoped<IReceiptItemRepository, ReceiptItemRepository>();
            services.AddScoped<IStoreRepository, StoreRepository>();
            services.AddScoped<IProductRepository, ProductRepository>();
            services.AddScoped<IMoneyMovementRepository, MoneyMovementRepository>();
            services.AddScoped<IMoneyMovementReceiptRepository, MoneyMovementReceiptRepository>();

            return services;
        }

        private static IServiceCollection AddInfrastructureConfig(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddOptions<ApiEndpointOptions>()
                .Bind(configuration.GetRequiredSection(ApiEndpointOptions.SectionName))
                .Validate(
                    options => Uri.TryCreate(options.ProverkachekaApiUrl, UriKind.Absolute, out _),
                    $"{ApiEndpointOptions.SectionName}:ProverkachekaApiUrl должен содержать абсолютный URL.")
                .ValidateOnStart();

            services.AddOptions<ProverkachekaOptions>()
                .Configure(options =>
                {
                    options.ProverkachekaApiToken = configuration[ProverkachekaOptions.SectionName] ?? string.Empty;
                })
                .Validate(
                    options => !string.IsNullOrWhiteSpace(options.ProverkachekaApiToken),
                    $"Не задана обязательная настройка {ProverkachekaOptions.SectionName}.")
                .ValidateOnStart();

            return services;
        }
    }
}
