using CostVision.Application.UseCases.Authorize.Authentication;
using CostVision.Application.UseCases.Authorize.Roles;
using CostVision.Application.UseCases.Authorize.Users;
using CostVision.Application.UseCases.Dashboard;
using CostVision.Application.UseCases.MoneyMovements;
using CostVision.Application.UseCases.MoneyMovements.BankStatementImports.Parsers.Alfa;
using CostVision.Application.UseCases.MoneyMovements.BankStatementImports.Parsers.Sber;
using CostVision.Application.UseCases.MoneyMovements.BankStatementImports.Parsers.TBank;
using CostVision.Application.UseCases.MoneyMovements.BankStatementImports.Parsing;
using CostVision.Application.UseCases.Receipts.Accounts;
using CostVision.Application.UseCases.Receipts.Products;
using CostVision.Application.UseCases.Receipts.Receipts;
using CostVision.Application.UseCases.Receipts.Receipts.Refresh;
using CostVision.Application.UseCases.Receipts.Stores;
using Microsoft.Extensions.DependencyInjection;

namespace CostVision.Application.Extensions
{
    public static class ApplicationServiceCollectionExtensions
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddScoped<IReceiptRefreshWorkflow, ReceiptRefreshWorkflow>();
            services.AddScoped<IGetDashboardIncomeExpenseReportUseCase, GetDashboardIncomeExpenseReportUseCase>();
            services.AddScoped<ISaveReceiptsScannedUseCase, SaveReceiptsScannedUseCase>();
            services.AddScoped<ISaveManualReceiptUseCase, SaveManualReceiptUseCase>();
            services.AddScoped<IGetReceiptListUseCase, GetReceiptListUseCase>();
            services.AddScoped<IGetReceiptListPageUseCase, GetReceiptListPageUseCase>();
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
            services.AddScoped<IGetProductListUseCase, GetProductListUseCase>();
            services.AddScoped<IUpdateProductAdaptiveNameUseCase, UpdateProductAdaptiveNameUseCase>();
            services.AddScoped<IGetStoreListUseCase, GetStoreListUseCase>();
            services.AddScoped<IUpdateStoreAdaptiveNameUseCase, UpdateStoreAdaptiveNameUseCase>();
            services.AddScoped<IGetUserListUseCase, GetUserListUseCase>();
            services.AddScoped<IGetUserUseCase, GetUserUseCase>();
            services.AddScoped<ICreateUserUseCase, CreateUserUseCase>();
            services.AddScoped<IUpdateUserUseCase, UpdateUserUseCase>();
            services.AddScoped<IToggleUserActiveUseCase, ToggleUserActiveUseCase>();
            services.AddScoped<IMarkUserActivityUseCase, MarkUserActivityUseCase>();
            services.AddScoped<IGetRoleListUseCase, GetRoleListUseCase>();
            services.AddScoped<IAuthenticateUserUseCase, AuthenticateUserUseCase>();
            services.AddScoped<IMoneyMovementsPageUseCase, MoneyMovementsPageUseCase>();
            services.AddScoped<ICreateMoneyMovementUseCase, CreateMoneyMovementUseCase>();
            services.AddScoped<IAutoLinkExactMoneyMovementReceiptsUseCase, AutoLinkExactMoneyMovementReceiptsUseCase>();
            services.AddScoped<IGetMoneyMovementListUseCase, GetMoneyMovementListUseCase>();
            services.AddScoped<IGetMoneyMovementAccountsUseCase, GetMoneyMovementAccountsUseCase>();
            services.AddScoped<IMoveMoneyMovementToAccountUseCase, MoveMoneyMovementToAccountUseCase>();
            services.AddScoped<IDeleteMoneyMovementUseCase, DeleteMoneyMovementUseCase>();
            services.AddScoped<IUpdateMoneyMovementCommentUseCase, UpdateMoneyMovementCommentUseCase>();
            services.AddScoped<IGetLinkedMoneyMovementReceiptsUseCase, GetLinkedMoneyMovementReceiptsUseCase>();
            services.AddScoped<IGetMoneyMovementReceiptCandidatesUseCase, GetMoneyMovementReceiptCandidatesUseCase>();
            services.AddScoped<IGetLinkedReceiptMoneyMovementsUseCase, GetLinkedReceiptMoneyMovementsUseCase>();
            services.AddScoped<IGetReceiptMoneyMovementCandidatesUseCase, GetReceiptMoneyMovementCandidatesUseCase>();
            services.AddScoped<ILinkMoneyMovementReceiptUseCase, LinkMoneyMovementReceiptUseCase>();
            services.AddScoped<IUnlinkMoneyMovementReceiptUseCase, UnlinkMoneyMovementReceiptUseCase>();
            services.AddScoped<IBankStatementParser, TBankPdfStatementParser>();
            services.AddScoped<IBankStatementParser, SberBankPdfStatementParser>();
            services.AddScoped<IBankStatementParser, AlfaBankPdfStatementParser>();
            services.AddScoped<IBankStatementParserRegistry, BankStatementParserRegistry>();
            services.AddScoped<IGetBankStatementImportBanksUseCase, GetBankStatementImportBanksUseCase>();
            services.AddScoped<IPreviewBankStatementImportUseCase, PreviewBankStatementImportUseCase>();
            services.AddScoped<IImportMoneyMovementsUseCase, ImportMoneyMovementsUseCase>();

            return services;
        }
    }
}
