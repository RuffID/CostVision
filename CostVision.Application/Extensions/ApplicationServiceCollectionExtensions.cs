using CostVision.Application.UseCases.Authorize.Authentication;
using CostVision.Application.UseCases.Authorize.Roles;
using CostVision.Application.UseCases.Authorize.Users;
using CostVision.Application.UseCases.MoneyMovements;
using CostVision.Application.UseCases.Receipts.Accounts;
using CostVision.Application.UseCases.Receipts.Receipts;
using CostVision.Application.UseCases.Receipts.Receipts.Refresh;
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
            services.AddScoped<ICreateMoneyMovementUseCase, CreateMoneyMovementUseCase>();
            services.AddScoped<IGetMoneyMovementListUseCase, GetMoneyMovementListUseCase>();
            services.AddScoped<IGetMoneyMovementAccountsUseCase, GetMoneyMovementAccountsUseCase>();
            services.AddScoped<IMoveMoneyMovementToAccountUseCase, MoveMoneyMovementToAccountUseCase>();
            services.AddScoped<IDeleteMoneyMovementUseCase, DeleteMoneyMovementUseCase>();
            services.AddScoped<IGetBankStatementImportBanksUseCase, GetBankStatementImportBanksUseCase>();
            services.AddScoped<IPreviewBankStatementImportUseCase, PreviewBankStatementImportUseCase>();
            services.AddScoped<IImportMoneyMovementsUseCase, ImportMoneyMovementsUseCase>();

            return services;
        }
    }
}
