using CostVision.Application.Abstractions.Service.Receipts;
using CostVision.Application.UseCases.Authorize;
using CostVision.Application.UseCases.Receipts;
using Microsoft.Extensions.DependencyInjection;

namespace CostVision.Application.Extensions
{
    public static class ApplicationServiceCollectionExtensions
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddScoped<IReceiptRefreshWorkflow, ReceiptRefreshWorkflow>();
            services.AddScoped<ISaveReceiptsScannedUseCase, SaveReceiptsScannedUseCase>();
            services.AddScoped<ISaveManualReceiptUseCase, SaveManualReceiptUseCase>();
            services.AddScoped<IGetReceiptListUseCase, GetReceiptListUseCase>();
            services.AddScoped<IGetReceiptWithItemsUseCase, GetReceiptWithItemsUseCase>();
            services.AddScoped<IRefreshReceiptFromApiUseCase, RefreshReceiptFromApiUseCase>();
            services.AddScoped<IRefreshReceiptsWithoutItemsUseCase, RefreshReceiptsWithoutItemsUseCase>();
            services.AddScoped<IDeleteReceiptUseCase, DeleteReceiptUseCase>();
            services.AddScoped<IGetUserAccountsUseCase, GetUserAccountsUseCase>();
            services.AddScoped<IGetUserAccountsForReceiptCreationUseCase, GetUserAccountsForReceiptCreationUseCase>();
            services.AddScoped<IValidateReceiptCreationAccessUseCase, ValidateReceiptCreationAccessUseCase>();
            services.AddScoped<ICreateAccountUseCase, CreateAccountUseCase>();
            services.AddScoped<IUpdateAccountUseCase, UpdateAccountUseCase>();
            services.AddScoped<IGetAccountShareUsersUseCase, GetAccountShareUsersUseCase>();
            services.AddScoped<IUpdateAccountMembersUseCase, UpdateAccountMembersUseCase>();
            services.AddScoped<IMoveReceiptToAccountUseCase, MoveReceiptToAccountUseCase>();
            services.AddScoped<IRemoveReceiptFromAccountUseCase, RemoveReceiptFromAccountUseCase>();
            services.AddScoped<IGetUserListUseCase, GetUserListUseCase>();
            services.AddScoped<IGetUserUseCase, GetUserUseCase>();
            services.AddScoped<ICreateUserUseCase, CreateUserUseCase>();
            services.AddScoped<IUpdateUserUseCase, UpdateUserUseCase>();
            services.AddScoped<IToggleUserActiveUseCase, ToggleUserActiveUseCase>();
            services.AddScoped<IGetRoleListUseCase, GetRoleListUseCase>();
            services.AddScoped<IAuthenticateUserUseCase, AuthenticateUserUseCase>();

            return services;
        }
    }
}
