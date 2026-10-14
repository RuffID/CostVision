using CostVision.Application.Abstractions.DataBase.Repositories.MoneyMovements;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.Abstractions.DataBase.Repositories.Authorization;
using CostVision.Application.Abstractions.DataBase.Repositories;
using System.Data;
using CostVision.Application.Abstractions.Service.Authorize;
using CostVision.Application.Abstractions.Service.MoneyMovements;
using CostVision.Application.Abstractions.Service.Receipts;
using CostVision.Application.UseCases.MoneyMovements.BankStatementImports.Parsing;
using CostVision.Application.UseCases.Authorize.Authentication;
using CostVision.Application.UseCases.Authorize.Roles;
using CostVision.Application.UseCases.Authorize.Users;
using CostVision.Application.UseCases.Dashboard;
using CostVision.Application.UseCases.MoneyMovements;
using CostVision.Application.UseCases.Receipts.Accounts;
using CostVision.Application.UseCases.Receipts.Products;
using CostVision.Application.UseCases.Receipts.Receipts;
using CostVision.Application.UseCases.Receipts.Receipts.Refresh;
using CostVision.Application.UseCases.Receipts.Stores;

namespace CostVision.Tests;

/// <summary>
/// Разворачивает общий тестовый набор в минимальные actual constructor-зависимости один раз.
/// </summary>
public static class TestUseCaseFactory
{
    public static GetStoreReceiptListUseCase CreateGetStoreReceiptListUseCase(ITestStorage storage)
    {
        return new GetStoreReceiptListUseCase(storage.Receipt, storage.MoneyMovement);
    }

    public static GetStoreListUseCase CreateGetStoreListUseCase(ITestStorage storage)
    {
        return new GetStoreListUseCase(storage.Store);
    }

    public static GetReceiptWithItemsUseCase CreateGetReceiptWithItemsUseCase(ITestStorage storage, IReceiptAccessVerificationService accessVerification)
    {
        return new GetReceiptWithItemsUseCase(storage.Receipt, accessVerification);
    }

    public static GetReceiptListUseCase CreateGetReceiptListUseCase(ITestStorage storage)
    {
        return new GetReceiptListUseCase(storage.Receipt);
    }

    public static GetReceiptListPageUseCase CreateGetReceiptListPageUseCase(ITestStorage storage)
    {
        return new GetReceiptListPageUseCase(storage.Receipt, storage.MoneyMovement);
    }

    public static RefreshReceiptFromApiUseCase CreateRefreshReceiptFromApiUseCase(ITestStorage storage, IReceiptAccessVerificationService accessVerification, IReceiptRefreshWorkflow receiptRefreshWorkflow)
    {
        return new RefreshReceiptFromApiUseCase(storage.Receipt, accessVerification, receiptRefreshWorkflow);
    }

    public static DeleteReceiptUseCase CreateDeleteReceiptUseCase(ITestStorage storage)
    {
        return new DeleteReceiptUseCase(storage, storage.Receipt);
    }

    public static GetProductListUseCase CreateGetProductListUseCase(ITestStorage storage)
    {
        return new GetProductListUseCase(storage.Product);
    }

    public static GetProductStorePurchasesUseCase CreateGetProductStorePurchasesUseCase(ITestStorage storage)
    {
        return new GetProductStorePurchasesUseCase(storage.ReceiptItem);
    }

    public static GetMoneyMovementListUseCase CreateGetMoneyMovementListUseCase(ITestStorage storage)
    {
        return new GetMoneyMovementListUseCase(storage.MoneyMovement, storage.Receipt);
    }

    public static GetMoneyMovementAccountsUseCase CreateGetMoneyMovementAccountsUseCase(ITestStorage storage)
    {
        return new GetMoneyMovementAccountsUseCase(storage.Account);
    }

    public static GetLinkedReceiptMoneyMovementsUseCase CreateGetLinkedReceiptMoneyMovementsUseCase(ITestStorage storage)
    {
        return new GetLinkedReceiptMoneyMovementsUseCase(storage.Receipt, storage.MoneyMovementReceipt);
    }

    public static GetLinkedMoneyMovementReceiptsUseCase CreateGetLinkedMoneyMovementReceiptsUseCase(ITestStorage storage)
    {
        return new GetLinkedMoneyMovementReceiptsUseCase(storage.MoneyMovement, storage.MoneyMovementReceipt);
    }

    public static GetUserAccountsUseCase CreateGetUserAccountsUseCase(ITestStorage storage)
    {
        return new GetUserAccountsUseCase(storage.Account);
    }

    public static GetAccountShareUsersUseCase CreateGetAccountShareUsersUseCase(ITestStorage storage)
    {
        return new GetAccountShareUsersUseCase(storage.Account, storage.User);
    }

    public static GetReceiptMoneyMovementCandidatesUseCase CreateGetReceiptMoneyMovementCandidatesUseCase(ITestStorage storage)
    {
        return new GetReceiptMoneyMovementCandidatesUseCase(storage.Receipt, storage.MoneyMovement);
    }

    public static GetMoneyMovementReceiptCandidatesUseCase CreateGetMoneyMovementReceiptCandidatesUseCase(ITestStorage storage)
    {
        return new GetMoneyMovementReceiptCandidatesUseCase(storage.MoneyMovement, storage.Receipt);
    }

    public static ValidateReceiptCreationAccessUseCase CreateValidateReceiptCreationAccessUseCase(ITestStorage storage)
    {
        return new ValidateReceiptCreationAccessUseCase(storage.AccountMember);
    }

    public static UpdateAccountUseCase CreateUpdateAccountUseCase(ITestStorage storage)
    {
        return new UpdateAccountUseCase(storage, storage.Account);
    }

    public static GetUserListUseCase CreateGetUserListUseCase(ITestStorage storage)
    {
        return new GetUserListUseCase(storage.User);
    }

    public static GetUserUseCase CreateGetUserUseCase(ITestStorage storage)
    {
        return new GetUserUseCase(storage.User);
    }

    public static PreviewBankStatementImportUseCase CreatePreviewBankStatementImportUseCase(ITestStorage storage,
        IBankStatementParserRegistry parserRegistry,
        IBankStatementPdfTextExtractor pdfTextExtractor)
    {
        return new PreviewBankStatementImportUseCase(storage.MoneyMovement, storage.AccountMember, parserRegistry, pdfTextExtractor);
    }

    public static GetRoleListUseCase CreateGetRoleListUseCase(ITestStorage storage)
    {
        return new GetRoleListUseCase(storage.Role);
    }

    public static AuthenticateUserUseCase CreateAuthenticateUserUseCase(ITestStorage storage, IPasswordHasher passwordHasher)
    {
        return new AuthenticateUserUseCase(storage, storage.User, passwordHasher);
    }

    public static MarkUserActivityUseCase CreateMarkUserActivityUseCase(ITestStorage storage)
    {
        return new MarkUserActivityUseCase(storage, storage.User);
    }

    public static GetActiveUserForRequestUseCase CreateGetActiveUserForRequestUseCase(ITestStorage storage)
    {
        return new GetActiveUserForRequestUseCase(storage.User);
    }

    public static ToggleUserActiveUseCase CreateToggleUserActiveUseCase(ITestStorage storage)
    {
        return new ToggleUserActiveUseCase(storage, storage.User);
    }

    public static GetDashboardIncomeExpenseReportUseCase CreateGetDashboardIncomeExpenseReportUseCase(ITestStorage storage)
    {
        return new GetDashboardIncomeExpenseReportUseCase(storage.Receipt, storage.MoneyMovement);
    }

    public static UpdateStoreAdaptiveNameUseCase CreateUpdateStoreAdaptiveNameUseCase(ITestStorage storage)
    {
        return new UpdateStoreAdaptiveNameUseCase(new StoreNamingUnitOfWorkAdapter(storage));
    }

    public static SaveReceiptsScannedUseCase CreateSaveReceiptsScannedUseCase(ITestStorage storage,
        IValidateReceiptCreationAccessUseCase validateReceiptCreationAccessUseCase,
        IQrParser qrParser)
    {
        return new SaveReceiptsScannedUseCase(new ReceiptCreationUnitOfWorkAdapter(storage), validateReceiptCreationAccessUseCase, qrParser);
    }

    public static SaveManualReceiptUseCase CreateSaveManualReceiptUseCase(ITestStorage storage,
        IValidateReceiptCreationAccessUseCase validateReceiptCreationAccessUseCase)
    {
        return new SaveManualReceiptUseCase(new ReceiptCreationUnitOfWorkAdapter(storage), validateReceiptCreationAccessUseCase);
    }

    public static UnlinkMoneyMovementReceiptUseCase CreateUnlinkMoneyMovementReceiptUseCase(ITestStorage storage)
    {
        return new UnlinkMoneyMovementReceiptUseCase(new MoneyMovementRemovalUnitOfWorkAdapter(storage));
    }

    public static ReceiptRefreshWorkflow CreateReceiptRefreshWorkflow(ITestStorage storage,
        IExternalReceiptProvider externalReceiptProvider,
        TimeProvider timeProvider)
    {
        return new ReceiptRefreshWorkflow(new ReceiptRefreshUnitOfWorkAdapter(storage), externalReceiptProvider, timeProvider);
    }

    public static LinkMoneyMovementReceiptUseCase CreateLinkMoneyMovementReceiptUseCase(ITestStorage storage)
    {
        return new LinkMoneyMovementReceiptUseCase(new ReceiptLinkUnitOfWorkAdapter(storage));
    }

    public static AutoLinkExactMoneyMovementReceiptsUseCase CreateAutoLinkExactMoneyMovementReceiptsUseCase(ITestStorage storage)
    {
        return new AutoLinkExactMoneyMovementReceiptsUseCase(new ReceiptReconciliationUnitOfWorkAdapter(storage));
    }

    public static UpdateProductAdaptiveNameUseCase CreateUpdateProductAdaptiveNameUseCase(ITestStorage storage)
    {
        return new UpdateProductAdaptiveNameUseCase(new ProductNamingUnitOfWorkAdapter(storage));
    }

    public static CreateAccountUseCase CreateCreateAccountUseCase(ITestStorage storage)
    {
        return new CreateAccountUseCase(new AccountCreationUnitOfWorkAdapter(storage));
    }

    public static UpdateMoneyMovementCommentUseCase CreateUpdateMoneyMovementCommentUseCase(ITestStorage storage)
    {
        return new UpdateMoneyMovementCommentUseCase(new MoneyMovementEditUnitOfWorkAdapter(storage));
    }

    public static MoveMoneyMovementToAccountUseCase CreateMoveMoneyMovementToAccountUseCase(ITestStorage storage)
    {
        return new MoveMoneyMovementToAccountUseCase(new MoneyMovementEditUnitOfWorkAdapter(storage));
    }

    public static DeleteMoneyMovementUseCase CreateDeleteMoneyMovementUseCase(ITestStorage storage)
    {
        return new DeleteMoneyMovementUseCase(new MoneyMovementRemovalUnitOfWorkAdapter(storage));
    }

    public static CreateMoneyMovementUseCase CreateCreateMoneyMovementUseCase(ITestStorage storage)
    {
        return new CreateMoneyMovementUseCase(new ReceiptLinkUnitOfWorkAdapter(storage));
    }

    public static RemoveReceiptFromAccountUseCase CreateRemoveReceiptFromAccountUseCase(ITestStorage storage)
    {
        return new RemoveReceiptFromAccountUseCase(new ReceiptRemovalUnitOfWorkAdapter(storage));
    }

    public static UpdateAccountMembersUseCase CreateUpdateAccountMembersUseCase(ITestStorage storage)
    {
        return new UpdateAccountMembersUseCase(new AccountMembershipUnitOfWorkAdapter(storage));
    }

    public static MoveReceiptToAccountUseCase CreateMoveReceiptToAccountUseCase(ITestStorage storage)
    {
        return new MoveReceiptToAccountUseCase(new ReceiptTransferUnitOfWorkAdapter(storage));
    }

    public static CreateUserUseCase CreateCreateUserUseCase(ITestStorage storage, IPasswordHasher passwordHasher)
    {
        return new CreateUserUseCase(new UserManagementUnitOfWorkAdapter(storage), passwordHasher);
    }

    public static UpdateUserUseCase CreateUpdateUserUseCase(ITestStorage storage, IPasswordHasher passwordHasher)
    {
        return new UpdateUserUseCase(new UserManagementUnitOfWorkAdapter(storage), passwordHasher);
    }

    public static ImportMoneyMovementsUseCase CreateImportMoneyMovementsUseCase(ITestStorage storage)
    {
        return new ImportMoneyMovementsUseCase(new BankStatementImportUnitOfWorkAdapter(storage));
    }

    private class TestScope(ITestStorage storage) : IUnitOfWorkScope
    {
        protected ITestStorage Storage { get; } = storage;

        public Task SaveChangesAsync(CancellationToken ct = default)
        {
            return Storage.SaveChangesAsync(ct);
        }

        public Task ExecuteInTransaction(Func<CancellationToken, Task> action, CancellationToken ct = default)
        {
            return Storage.ExecuteInTransaction(action, ct);
        }

        public Task ExecuteInTransaction(Func<CancellationToken, Task> action, IsolationLevel isolationLevel, CancellationToken ct = default)
        {
            return Storage.ExecuteInTransaction(action, isolationLevel, ct);
        }
    }

    private class UserManagementUnitOfWorkAdapter(ITestStorage storage) : TestScope(storage), IUserManagementUnitOfWork
    {
        public IUserRepository User => Storage.User;
        public IRoleRepository Role => Storage.Role;
    }

    private class AccountCreationUnitOfWorkAdapter(ITestStorage storage) : TestScope(storage), IAccountCreationUnitOfWork
    {
        public IAccountRepository Account => Storage.Account;
        public IAccountMemberRepository AccountMember => Storage.AccountMember;
    }

    private class AccountMembershipUnitOfWorkAdapter(ITestStorage storage) : TestScope(storage), IAccountMembershipUnitOfWork
    {
        public IAccountRepository Account => Storage.Account;
        public IAccountMemberRepository AccountMember => Storage.AccountMember;
        public IUserRepository User => Storage.User;
    }

    private class ReceiptCreationUnitOfWorkAdapter(ITestStorage storage) : TestScope(storage), IReceiptCreationUnitOfWork
    {
        public IReceiptRepository Receipt => Storage.Receipt;
        public IReceiptAccountRepository ReceiptAccount => Storage.ReceiptAccount;
    }

    private class ReceiptTransferUnitOfWorkAdapter(ITestStorage storage) : TestScope(storage), IReceiptTransferUnitOfWork
    {
        public IReceiptRepository Receipt => Storage.Receipt;
        public IReceiptAccountRepository ReceiptAccount => Storage.ReceiptAccount;
        public IAccountRepository Account => Storage.Account;
    }

    private class ReceiptRemovalUnitOfWorkAdapter(ITestStorage storage) : TestScope(storage), IReceiptRemovalUnitOfWork
    {
        public IReceiptRepository Receipt => Storage.Receipt;
        public IReceiptAccountRepository ReceiptAccount => Storage.ReceiptAccount;
    }

    private class ReceiptRefreshUnitOfWorkAdapter(ITestStorage storage) : TestScope(storage), IReceiptRefreshUnitOfWork
    {
        public IReceiptRepository Receipt => Storage.Receipt;
        public IProductRepository Product => Storage.Product;
        public IStoreRepository Store => Storage.Store;
    }

    private class ProductNamingUnitOfWorkAdapter(ITestStorage storage) : TestScope(storage), IProductNamingUnitOfWork
    {
        public IProductRepository Product => Storage.Product;
        public IReceiptRepository Receipt => Storage.Receipt;
    }

    private class StoreNamingUnitOfWorkAdapter(ITestStorage storage) : TestScope(storage), IStoreNamingUnitOfWork
    {
        public IStoreRepository Store => Storage.Store;
        public IReceiptRepository Receipt => Storage.Receipt;
    }

    private class BankStatementImportUnitOfWorkAdapter(ITestStorage storage) : TestScope(storage), IBankStatementImportUnitOfWork
    {
        public IMoneyMovementRepository MoneyMovement => Storage.MoneyMovement;
        public IAccountMemberRepository AccountMember => Storage.AccountMember;
    }

    private class MoneyMovementEditUnitOfWorkAdapter(ITestStorage storage) : TestScope(storage), IMoneyMovementEditUnitOfWork
    {
        public IMoneyMovementRepository MoneyMovement => Storage.MoneyMovement;
        public IAccountMemberRepository AccountMember => Storage.AccountMember;
    }

    private class MoneyMovementRemovalUnitOfWorkAdapter(ITestStorage storage) : TestScope(storage), IMoneyMovementRemovalUnitOfWork
    {
        public IMoneyMovementRepository MoneyMovement => Storage.MoneyMovement;
        public IMoneyMovementReceiptRepository MoneyMovementReceipt => Storage.MoneyMovementReceipt;
        public IAccountMemberRepository AccountMember => Storage.AccountMember;
    }

    private class ReceiptLinkUnitOfWorkAdapter(ITestStorage storage) : TestScope(storage), IReceiptLinkUnitOfWork
    {
        public IMoneyMovementRepository MoneyMovement => Storage.MoneyMovement;
        public IReceiptRepository Receipt => Storage.Receipt;
        public IMoneyMovementReceiptRepository MoneyMovementReceipt => Storage.MoneyMovementReceipt;
        public IAccountMemberRepository AccountMember => Storage.AccountMember;
    }

    private class ReceiptReconciliationUnitOfWorkAdapter(ITestStorage storage) : TestScope(storage), IReceiptReconciliationUnitOfWork
    {
        public IMoneyMovementRepository MoneyMovement => Storage.MoneyMovement;
        public IReceiptRepository Receipt => Storage.Receipt;
        public IMoneyMovementReceiptRepository MoneyMovementReceipt => Storage.MoneyMovementReceipt;
    }
}
