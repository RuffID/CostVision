using EFCoreLibrary.Maintenance.Models;
using EFCoreLibrary.Maintenance.SqlServer.Extensions;
using EFCoreLibrary.Maintenance.SqlServer.Options;
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
using EFCoreLibrary.Extensions;
using HttpClientLibrary.Abstractions;
using HttpClientLibrary.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

            services.AddSingleton<SafeDatabaseLoggerFactory>();
            services.AddDbContext<ApplicationContext>((provider, options) => options
                .UseSqlServer(connectionString)
                .UseLoggerFactory(provider.GetRequiredService<SafeDatabaseLoggerFactory>()));
            services.AddEfCoreContext<ApplicationContext, CostVisionContextKey>();
            services.AddEfCoreBaseRepositories<CostVisionContextKey>();
            services.AddEfCoreSqlServerMaintenance<CostVisionContextKey>(
                new SqlServerMaintenanceOptions(configuration[$"{DatabaseMaintenanceSettings.SECTION_NAME}:ServerBackupDirectory"] ?? string.Empty),
                MaintenanceExecutionMode.SingleInitializer);
            services.AddSingleton<DatabaseMaintenanceStartup>();

            services.AddHttpClient<IHttpApiClient, HttpApiClient>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(180);
            });

            services.AddScoped<Hasher>();
            services.AddScoped<IPasswordHasher>(sp => sp.GetRequiredService<Hasher>());
            services.AddScoped<IQrParser, QrParser>();
            services.AddScoped<IReceiptAccessVerificationService, ReceiptAccessVerificationService>();
            services.AddScoped<IReceiptRequest, ReceiptRequest>();
            services.AddScoped<IExternalReceiptProvider, ExternalReceiptProvider>();
            services.AddScoped<IBankStatementPdfTextExtractor, BankStatementPdfTextExtractor>();

            services.AddSingleton<IReceiptRefreshBackgroundScheduler, MidnightReceiptRefreshBackgroundScheduler>();
            services.AddHostedService<ReceiptRefreshBackgroundService>();

            services.AddScoped<IUnitOfWorkScope, UnitOfWorkScope>();
            services.AddScoped<IUserManagementUnitOfWork, UserManagementUnitOfWork>();
            services.AddScoped<IAccountCreationUnitOfWork, AccountCreationUnitOfWork>();
            services.AddScoped<IAccountMembershipUnitOfWork, AccountMembershipUnitOfWork>();
            services.AddScoped<IReceiptCreationUnitOfWork, ReceiptCreationUnitOfWork>();
            services.AddScoped<IReceiptTransferUnitOfWork, ReceiptTransferUnitOfWork>();
            services.AddScoped<IReceiptRemovalUnitOfWork, ReceiptRemovalUnitOfWork>();
            services.AddScoped<IReceiptRefreshUnitOfWork, ReceiptRefreshUnitOfWork>();
            services.AddScoped<IProductNamingUnitOfWork, ProductNamingUnitOfWork>();
            services.AddScoped<IStoreNamingUnitOfWork, StoreNamingUnitOfWork>();
            services.AddScoped<IBankStatementImportUnitOfWork, BankStatementImportUnitOfWork>();
            services.AddScoped<IMoneyMovementEditUnitOfWork, MoneyMovementEditUnitOfWork>();
            services.AddScoped<IMoneyMovementRemovalUnitOfWork, MoneyMovementRemovalUnitOfWork>();
            services.AddScoped<IReceiptLinkUnitOfWork, ReceiptLinkUnitOfWork>();
            services.AddScoped<IReceiptReconciliationUnitOfWork, ReceiptReconciliationUnitOfWork>();
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
            services.AddOptions<DatabaseMaintenanceSettings>()
                .Bind(configuration.GetSection(DatabaseMaintenanceSettings.SECTION_NAME))
                .Validate(settings => settings.TimeoutSeconds is > 0 and <= 3600, "DatabaseMaintenance: invalid timeout.")
                .Validate(settings => Enum.IsDefined(settings.Operation), "DatabaseMaintenance: invalid operation.")
                .Validate(settings => settings.SingleInitializerConfirmed, "DatabaseMaintenance: SingleInitializer must be confirmed by the operator.")
                .Validate(settings => !string.IsNullOrWhiteSpace(settings.ServerBackupDirectory), "DatabaseMaintenance: server backup directory is required.")
                .ValidateOnStart();

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
