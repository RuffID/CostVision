using CostVision.Tests;
using System.Linq.Expressions;
using System.Reflection;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Authorization;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.Abstractions.DataBase.Repositories.MoneyMovements;
using CostVision.Application.Abstractions.Service.Receipts;
using CostVision.Domain.Models.MoneyMovements;
using CostVision.Application.Abstractions.Service.Authorize;
using CostVision.Application.Models.Requests.Authorize;
using CostVision.Application.Models.Requests.MoneyMovements;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.Authorize.Authentication;
using CostVision.Application.UseCases.Authorize.Users;
using CostVision.Application.UseCases.MoneyMovements;
using CostVision.Application.UseCases.Receipts.Accounts;
using CostVision.Application.UseCases.Receipts.Products;
using CostVision.Application.UseCases.Receipts.Receipts;
using CostVision.Application.UseCases.Receipts.Stores;
using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.Enums.Authorization;
using CostVision.Domain.Models.Enums.MoneyMovements;
using CostVision.Domain.Models.Receipts;
using Moq;
using Xunit;

namespace CostVision.Application.UnitTests;

public class Stage02RegressionTests
{
    [Theory]
    [InlineData(false, RoleType.User)]
    [InlineData(true, RoleType.User)]
    [InlineData(false, RoleType.Admin)]
    [InlineData(true, RoleType.Admin)]
    public async Task UnavailableCatalog_CannotBeRenamedOrSaved(bool store, RoleType globalRole)
    {
        // Arrange
        Fixture data = new();
        Assert.True(data.Role.TryUpdateDetails(globalRole.ToString(), globalRole, out _));
        data.AddPurchase(Guid.NewGuid());
        string? previousName = data.Product.AdaptiveName;

        // Act
        ServiceResult result;
        if (store)
        {
            result = await TestUseCaseFactory.CreateUpdateStoreAdaptiveNameUseCase(data.Work.Object)
                .ExecuteAsync(new() { StoreId = data.Store.Id, AdaptiveName = "Changed" }, data.User.Id, CancellationToken.None);
        }
        else
        {
            result = await TestUseCaseFactory.CreateUpdateProductAdaptiveNameUseCase(data.Work.Object)
                .ExecuteAsync(new() { ProductId = data.Product.Id, AdaptiveName = "Changed" }, data.User.Id, CancellationToken.None);
        }

        // Assert
        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.Forbidden, result.Error?.Type);
        Assert.Equal(previousName, data.Product.AdaptiveName);
        Assert.Null(data.Store.AdaptiveName);
        data.AssertNoWrite();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task UnknownRole_DoesNotChangeProfilePasswordOrRoles(bool update, bool emptyId)
    {
        // Arrange
        Fixture data = new();
        Guid[] oldRoles = data.User.UserRoles.Select(role => role.RoleId).ToArray();
        Guid unknownId = Guid.NewGuid();
        if (emptyId)
        {
            unknownId = Guid.Empty;
        }

        UserUpsertRequest request = new()
        {
            Id = data.User.Id, Login = "changed", Name = "Changed",
            Password = "Changed1!", RoleIds = [data.Role.Id, unknownId]
        };

        // Act
        ServiceResult result;
        if (update)
        {
            result = await TestUseCaseFactory.CreateUpdateUserUseCase(data.Work.Object, data.Hasher.Object).ExecuteAsync(request, CancellationToken.None);
        }
        else
        {
            result = await TestUseCaseFactory.CreateCreateUserUseCase(data.Work.Object, data.Hasher.Object).ExecuteAsync(request, CancellationToken.None);
        }

        // Assert
        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.Validation, result.Error?.Type);
        Assert.Equal("user", data.User.Login);
        Assert.Equal("User", data.User.Name);
        Assert.Equal("existing-hash", data.User.PasswordHash);
        Assert.Equal(oldRoles, data.User.UserRoles.Select(role => role.RoleId));
        data.Users.Verify(repository => repository.Create(It.IsAny<User>()), Times.Never);
        data.Hasher.Verify(hasher => hasher.Hash(It.IsAny<string>()), Times.Never);
        data.AssertNoWrite();
    }

    [Fact]
    public async Task Login_NormalizesWhitespaceAndCase_VerifiesExistingHashOnce()
    {
        // Arrange
        Fixture data = new();

        // Act
        var result = await TestUseCaseFactory.CreateAuthenticateUserUseCase(data.Work.Object, data.Hasher.Object)
            .ExecuteAsync(new() { Login = " USER ", Password = " Password1! " }, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(data.User.Id, result.Data?.Id);
        Assert.Equal("existing-hash", data.User.PasswordHash);
        data.Hasher.Verify(hasher => hasher.Verify("Password1!", "existing-hash"), Times.Once);
        data.Hasher.Verify(hasher => hasher.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        data.Hasher.Verify(hasher => hasher.Hash(It.IsAny<string>()), Times.Never);
        data.Work.Verify(work => work.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(AccountAccessRole.Owner)]
    [InlineData(AccountAccessRole.Editor)]
    public async Task ArchivedAccount_RejectsReceiptCreation(AccountAccessRole role)
    {
        // Arrange
        Fixture data = new(role);
        data.Account.Archive();

        // Act
        ServiceResult result = await TestUseCaseFactory.CreateValidateReceiptCreationAccessUseCase(data.Work.Object)
            .ExecuteAsync(data.Account.Id, data.User.Id, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.Forbidden, result.Error?.Type);
        data.AssertNoWrite();
    }

    [Theory]
    [InlineData(AccountAccessRole.Owner, false, false)]
    [InlineData(AccountAccessRole.Editor, false, false)]
    [InlineData(AccountAccessRole.Owner, true, false)]
    [InlineData(AccountAccessRole.Editor, true, false)]
    [InlineData(AccountAccessRole.Owner, true, true)]
    [InlineData(AccountAccessRole.Editor, true, true)]
    public async Task ArchivedAccount_RejectsMovementAndImportBeforeTransaction(AccountAccessRole role, bool import, bool replaceDuplicate)
    {
        // Arrange
        Fixture data = new(role);
        data.Account.Archive();
        MoneyMovement existing = TestMoneyMovementFactory.CreateBankStatementImport(accountId: data.Account.Id, amount: 20);
        string? previousComment = existing.ImportComment;
        Mock<IMoneyMovementRepository> movements = new(MockBehavior.Strict);
        data.Work.Setup(work => work.MoneyMovement).Returns(movements.Object);

        // Act
        bool success;
        ServiceErrorType? error;
        if (import)
        {
            var result = await TestUseCaseFactory.CreateImportMoneyMovementsUseCase(data.Work.Object).ExecuteAsync(new()
            {
                AccountId = data.Account.Id,
                Rows = [new()
                {
                    Amount = 10, Type = MoneyMovementType.Expense, OccurredAt = DateTime.Today,
                    ImportComment = "statement", ReplaceDuplicate = replaceDuplicate, DuplicateMoneyMovementId = existing.Id
                }]
            }, data.User.Id, CancellationToken.None);
            success = result.Success;
            error = result.Error?.Type;
        }
        else
        {
            var result = await TestUseCaseFactory.CreateCreateMoneyMovementUseCase(data.Work.Object).ExecuteAsync(new()
            {
                AccountId = data.Account.Id, Amount = 10, Type = MoneyMovementType.Expense, OccurredAt = DateTime.Today
            }, data.User.Id, CancellationToken.None);
            success = result.Success;
            error = result.Error?.Type;
        }

        // Assert
        Assert.False(success);
        Assert.Equal(ServiceErrorType.Forbidden, error);
        Assert.Equal(20, existing.Amount);
        Assert.Equal(previousComment, existing.ImportComment);
        movements.VerifyNoOtherCalls();
        data.AssertNoWrite();
    }

    [Theory]
    [InlineData(false, "missing")]
    [InlineData(true, "missing")]
    [InlineData(false, "wrong-role")]
    [InlineData(true, "wrong-role")]
    [InlineData(false, "duplicate")]
    [InlineData(true, "duplicate")]
    [InlineData(false, "other-owner")]
    [InlineData(true, "other-owner")]
    public async Task InvalidOwnerMembership_IsAnExplicitInvariantFailure(bool moneyAccounts, string corruption)
    {
        // Arrange
        Fixture data = new(AccountAccessRole.Owner);
        FieldInfo field = typeof(Account).GetField("_members", BindingFlags.Instance | BindingFlags.NonPublic)!;
        List<AccountMember> members = (List<AccountMember>)field.GetValue(data.Account)!;
        AccountMember owner = Assert.Single(members);
        if (corruption == "missing")
        {
            members.Clear();
        }
        else if (corruption == "wrong-role")
        {
            typeof(AccountMember).GetProperty(nameof(AccountMember.Role))!.SetValue(owner, AccountAccessRole.Editor);
        }
        else if (corruption == "duplicate")
        {
            members.Add(owner);
        }
        else
        {
            AccountMember other = TestAccountFactory.CreateMember(data.Account.Id, Guid.NewGuid(), AccountAccessRole.Editor);
            typeof(AccountMember).GetProperty(nameof(AccountMember.Role))!.SetValue(other, AccountAccessRole.Owner);
            members.Add(other);
        }

        // Act
        Func<Task> action;
        if (moneyAccounts)
        {
            action = async () => await TestUseCaseFactory.CreateGetMoneyMovementAccountsUseCase(data.Work.Object).ExecuteAsync(data.User.Id, CancellationToken.None);
        }
        else
        {
            action = async () => await TestUseCaseFactory.CreateGetUserAccountsUseCase(data.Work.Object).ExecuteAsync(data.User.Id, true, CancellationToken.None);
        }

        // Assert
        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(action);
        Assert.Contains("Owner", error.Message);
        data.AssertNoWrite();
    }

    [Theory]
    [InlineData(false, AccountAccessRole.Owner)]
    [InlineData(false, AccountAccessRole.Editor)]
    [InlineData(false, AccountAccessRole.Viewer)]
    [InlineData(true, AccountAccessRole.Owner)]
    [InlineData(true, AccountAccessRole.Editor)]
    [InlineData(true, AccountAccessRole.Viewer)]
    public async Task Catalog_WithAccessiblePurchase_CanRenameAndClear(bool store, AccountAccessRole role)
    {
        // Arrange
        Fixture data = new(role);
        Receipt purchase = data.AddPurchase(Guid.NewGuid());
        Assert.True(purchase.TryAddAccount(data.Account, out _, out _));

        // Act
        ServiceResult result = await data.Rename(store, " Common name ");

        // Assert
        Assert.True(result.Success);
        if (store)
        {
            Assert.Equal("Common name", data.Store.AdaptiveName);
        }
        else
        {
            Assert.Equal("Common name", data.Product.AdaptiveName);
        }

        data.Work.Verify(work => work.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        // Act
        ServiceResult cleared = await data.Rename(store, " ");

        // Assert
        Assert.True(cleared.Success);
        if (store)
        {
            Assert.Null(data.Store.AdaptiveName);
        }
        else
        {
            Assert.Null(data.Product.AdaptiveName);
        }

        data.Work.Verify(work => work.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Catalog_AfterMembershipRemoval_DeniesMutationButKeepsOwnPurchaseAccess(bool store)
    {
        // Arrange
        Fixture data = new(AccountAccessRole.Editor);
        Receipt purchase = data.AddPurchase(Guid.NewGuid());
        Assert.True(purchase.TryAddAccount(data.Account, out _, out _));
        Assert.True(data.Account.TryRemoveMember(data.User.Id, out _, out _));

        // Act
        ServiceResult denied = await data.Rename(store, "Changed");

        // Assert
        Assert.False(denied.Success);
        Assert.Equal(ServiceErrorType.Forbidden, denied.Error?.Type);
        Assert.Equal("Common", data.Product.AdaptiveName);
        Assert.Null(data.Store.AdaptiveName);
        data.AssertNoWrite();

        // Arrange
        data.AddPurchase(data.User.Id);

        // Act
        ServiceResult ownPurchase = await data.Rename(store, "Own purchase");

        // Assert
        Assert.True(ownPurchase.Success);
        data.Work.Verify(work => work.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Catalog_UnrelatedOwnPurchase_DoesNotGrantObjectAccess(bool store)
    {
        // Arrange
        Fixture data = new();
        Store unrelatedStore = TestReceiptFactory.CreateStore("Other shop");
        unrelatedStore.Id = Guid.NewGuid();
        Product unrelatedProduct = TestReceiptFactory.CreateProduct("Other product");
        unrelatedProduct.Id = Guid.NewGuid();
        Receipt receipt = TestReceiptFactory.Create(createdByUserId: data.User.Id, store: unrelatedStore);
        Assert.True(receipt.TryAddItem(TestReceiptFactory.CreateItem(unrelatedProduct), out _));
        data.Receipts.Add(receipt);

        // Act
        ServiceResult result = await data.Rename(store, "Changed");

        // Assert
        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.Forbidden, result.Error?.Type);
        Assert.Equal("Common", data.Product.AdaptiveName);
        Assert.Null(data.Store.AdaptiveName);
        data.AssertNoWrite();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EmptyUpdatePassword_PreservesHashAndUpdatesKnownRoles(string password)
    {
        // Arrange
        Fixture data = new();
        UserUpsertRequest request = new()
        {
            Id = data.User.Id, Login = " USER ", Name = " New Name ",
            Password = password, RoleIds = [data.Role.Id, data.Role.Id]
        };

        // Act
        ServiceResult result = await TestUseCaseFactory.CreateUpdateUserUseCase(data.Work.Object, data.Hasher.Object)
            .ExecuteAsync(request, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("USER", data.User.Login);
        Assert.Equal("New Name", data.User.Name);
        Assert.Equal("existing-hash", data.User.PasswordHash);
        Assert.Equal(data.Role.Id, Assert.Single(data.User.UserRoles).RoleId);
        data.Hasher.Verify(hasher => hasher.Hash(It.IsAny<string>()), Times.Never);
        data.Work.Verify(work => work.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InternalPasswordSpaces_AreRejectedBeforeAnyMutation(bool update)
    {
        // Arrange
        Fixture data = new();
        UserUpsertRequest request = new()
        {
            Id = data.User.Id, Login = "changed", Name = "Changed",
            Password = "Pass word1!", RoleIds = [data.Role.Id]
        };

        // Act
        ServiceResult result;
        if (update)
        {
            result = await TestUseCaseFactory.CreateUpdateUserUseCase(data.Work.Object, data.Hasher.Object).ExecuteAsync(request, CancellationToken.None);
        }
        else
        {
            result = await TestUseCaseFactory.CreateCreateUserUseCase(data.Work.Object, data.Hasher.Object).ExecuteAsync(request, CancellationToken.None);
        }

        // Assert
        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.Validation, result.Error?.Type);
        Assert.Equal("user", data.User.Login);
        Assert.Equal("existing-hash", data.User.PasswordHash);
        Assert.Equal(data.Role.Id, Assert.Single(data.User.UserRoles).RoleId);
        data.Users.Verify(repository => repository.Create(It.IsAny<User>()), Times.Never);
        data.Hasher.Verify(hasher => hasher.Hash(It.IsAny<string>()), Times.Never);
        data.AssertNoWrite();
    }

    [Fact]
    public async Task WrongPassword_IsVerifiedOnceWithoutSavingOrRehashing()
    {
        // Arrange
        Fixture data = new();

        // Act
        var result = await TestUseCaseFactory.CreateAuthenticateUserUseCase(data.Work.Object, data.Hasher.Object)
            .ExecuteAsync(new() { Login = " USER ", Password = " Wrong1! " }, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.Unauthorized, result.Error?.Type);
        Assert.Null(data.User.LastLoginAtUtc);
        Assert.Equal("existing-hash", data.User.PasswordHash);
        data.Hasher.Verify(hasher => hasher.Verify("Wrong1!", "existing-hash"), Times.Once);
        data.Hasher.Verify(hasher => hasher.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        data.Hasher.Verify(hasher => hasher.Hash(It.IsAny<string>()), Times.Never);
        data.AssertNoWrite();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ArchivedAccount_ManualAndQrUseRealCreationValidator(bool scanned)
    {
        // Arrange
        Fixture data = new(AccountAccessRole.Owner);
        data.Account.Archive();
        ValidateReceiptCreationAccessUseCase access = TestUseCaseFactory.CreateValidateReceiptCreationAccessUseCase(data.Work.Object);
        Mock<IQrParser> parser = new(MockBehavior.Strict);

        // Act
        bool success;
        ServiceErrorType? error;
        if (scanned)
        {
            var result = await TestUseCaseFactory.CreateSaveReceiptsScannedUseCase(data.Work.Object, access, parser.Object)
                .ExecuteAsync(new() { AccountId = data.Account.Id, Results = [new() { DecodedText = "qr" }] },
                    data.User.Id, CancellationToken.None);
            success = result.Success;
            error = result.Error?.Type;
        }
        else
        {
            var result = await TestUseCaseFactory.CreateSaveManualReceiptUseCase(data.Work.Object, access)
                .ExecuteAsync(new() { AccountId = data.Account.Id }, data.User.Id, CancellationToken.None);
            success = result.Success;
            error = result.Error?.Type;
        }

        // Assert
        Assert.False(success);
        Assert.Equal(ServiceErrorType.Forbidden, error);
        parser.VerifyNoOtherCalls();
        data.AssertNoWrite();
    }

    [Fact]
    public async Task ArchivedAccount_PreservesHistoricalCommentEditingAndExplicitReading()
    {
        // Arrange
        Fixture data = new(AccountAccessRole.Editor);
        data.Account.Archive();
        MoneyMovement movement = TestMoneyMovementFactory.CreateManual(accountId: data.Account.Id, userId: data.User.Id);
        Mock<IMoneyMovementRepository> movements = new(MockBehavior.Strict);
        movements.Setup(repository => repository.GetItemByPredicateAsync(
            It.IsAny<Expression<Func<MoneyMovement, bool>>>(),
            false, null, It.IsAny<CancellationToken>()))
            .Returns((Expression<Func<MoneyMovement, bool>> predicate,
                bool tracking, Func<IQueryable<MoneyMovement>, IQueryable<MoneyMovement>> include, CancellationToken ct) =>
                Task.FromResult(new[] { movement }.FirstOrDefault(predicate.Compile())));
        data.Work.Setup(work => work.MoneyMovement).Returns(movements.Object);

        // Act
        ServiceResult result = await TestUseCaseFactory.CreateUpdateMoneyMovementCommentUseCase(data.Work.Object).ExecuteAsync(new()
        {
            AccountId = data.Account.Id, MoneyMovementId = movement.Id, Comment = "Historical"
        }, data.User.Id, CancellationToken.None);
        var accounts = await TestUseCaseFactory.CreateGetUserAccountsUseCase(data.Work.Object).ExecuteAsync(data.User.Id, true, CancellationToken.None);
        var activeAccounts = await TestUseCaseFactory.CreateGetMoneyMovementAccountsUseCase(data.Work.Object).ExecuteAsync(data.User.Id, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Historical", movement.Comment);
        Assert.True(accounts.Success);
        Assert.False(Assert.Single(accounts.Data!).IsActive);
        Assert.Equal(AccountAccessRole.Editor, Assert.Single(accounts.Data!).AccessRole);
        Assert.Empty(activeAccounts);
        data.Work.Verify(work => work.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AllKnownRoles_AreAssignedTogetherAfterValidation(bool update)
    {
        // Arrange
        Fixture data = new();
        Assert.True(Role.TryCreate("Admin", RoleType.Admin, out Role? admin, out _));
        admin!.Id = Guid.NewGuid();
        data.AvailableRoles.Add(admin);
        UserUpsertRequest request = new()
        {
            Id = data.User.Id, Login = "changed", Name = "Changed",
            Password = " Password1! ", RoleIds = [data.Role.Id, admin.Id, data.Role.Id]
        };

        // Act
        ServiceResult result;
        if (update)
        {
            result = await TestUseCaseFactory.CreateUpdateUserUseCase(data.Work.Object, data.Hasher.Object).ExecuteAsync(request, CancellationToken.None);
        }
        else
        {
            result = await TestUseCaseFactory.CreateCreateUserUseCase(data.Work.Object, data.Hasher.Object).ExecuteAsync(request, CancellationToken.None);
        }

        // Assert
        Assert.True(result.Success);
        User target = data.User;
        if (!update)
        {
            Assert.NotNull(data.CreatedUser);
            target = data.CreatedUser;
        }

        Assert.Equal("changed", target.Login);
        Assert.Equal("Changed", target.Name);
        Assert.Equal("new-hash", target.PasswordHash);
        Assert.Equal(new[] { data.Role.Id, admin.Id }.Order(), target.UserRoles.Select(role => role.RoleId).Order());
        data.Hasher.Verify(hasher => hasher.Hash("Password1!"), Times.Once);
        data.Work.Verify(work => work.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(AccountAccessRole.Owner)]
    [InlineData(AccountAccessRole.Editor)]
    [InlineData(AccountAccessRole.Viewer)]
    public async Task AccountReading_AgreesAcrossListsAndRevokesRemovedMembership(AccountAccessRole role)
    {
        // Arrange
        Fixture data = new(role);

        // Act
        var receiptAccounts = await TestUseCaseFactory.CreateGetUserAccountsUseCase(data.Work.Object).ExecuteAsync(data.User.Id, false, CancellationToken.None);
        var moneyAccounts = await TestUseCaseFactory.CreateGetMoneyMovementAccountsUseCase(data.Work.Object).ExecuteAsync(data.User.Id, CancellationToken.None);

        // Assert
        Assert.True(receiptAccounts.Success);
        Assert.Equal(data.Account.Id, Assert.Single(receiptAccounts.Data!).Id);
        Assert.Equal(role, Assert.Single(receiptAccounts.Data!).AccessRole);
        Assert.Equal(data.Account.Id, Assert.Single(moneyAccounts).Id);
        Assert.Equal(role, Assert.Single(moneyAccounts).AccessRole);
        data.AssertNoWrite();

        if (role == AccountAccessRole.Owner)
        {
            return;
        }

        // Arrange
        Assert.True(data.Account.TryRemoveMember(data.User.Id, out _, out _));

        // Act
        var removedReceiptAccounts = await TestUseCaseFactory.CreateGetUserAccountsUseCase(data.Work.Object).ExecuteAsync(data.User.Id, false, CancellationToken.None);
        var removedMoneyAccounts = await TestUseCaseFactory.CreateGetMoneyMovementAccountsUseCase(data.Work.Object).ExecuteAsync(data.User.Id, CancellationToken.None);

        // Assert
        Assert.True(removedReceiptAccounts.Success);
        Assert.Empty(removedReceiptAccounts.Data!);
        Assert.Empty(removedMoneyAccounts);
        data.AssertNoWrite();
    }

    private class Fixture
    {
        public User User { get; } = TestUserFactory.Create(passwordHash: "existing-hash");
        public Role Role { get; }
        public List<Role> AvailableRoles { get; } = [];
        public User? CreatedUser { get; private set; }
        public Account Account { get; }
        public Product Product { get; } = TestReceiptFactory.CreateProduct("Milk", "Common");
        public Store Store { get; } = TestReceiptFactory.CreateStore("Shop");
        public List<Receipt> Receipts { get; } = [];
        public Mock<ITestStorage> Work { get; } = new(MockBehavior.Strict);
        public Mock<IUserRepository> Users { get; } = new(MockBehavior.Strict);
        public Mock<IPasswordHasher> Hasher { get; } = new(MockBehavior.Strict);

        public Fixture(AccountAccessRole accountRole = AccountAccessRole.Viewer)
        {
            Role = User.Roles.Single();
            AvailableRoles.Add(Role);
            Product.Id = Guid.NewGuid();
            Store.Id = Guid.NewGuid();
            if (accountRole == AccountAccessRole.Owner)
            {
                Account = TestAccountFactory.Create(owner: User);
            }
            else
            {
                Account = TestAccountFactory.Create(owner: TestUserFactory.Create());
                Assert.True(Account.TryAddMember(User.Id, accountRole, out _, out _));
            }

            Mock<IProductRepository> products = new(MockBehavior.Strict);
            // Имитируем fixup внешнего ключа после назначения ID сохранённому счёту.
            typeof(AccountMember).GetProperty(nameof(AccountMember.AccountId))!
                .SetValue(Account.Members.Single(member => member.Role == AccountAccessRole.Owner), Account.Id);

            products.Setup(repository => repository.GetItemByIdAsync(It.IsAny<Guid>(), It.IsAny<bool>(),
                It.IsAny<Func<IQueryable<Product>, IQueryable<Product>>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Product);
            products.Setup(repository => repository.GetItemByPredicateAsync(It.IsAny<Expression<Func<Product, bool>>>(),
                It.IsAny<bool>(), It.IsAny<Func<IQueryable<Product>, IQueryable<Product>>>(), It.IsAny<CancellationToken>()))
                .Returns((Expression<Func<Product, bool>> predicate, bool tracking,
                    Func<IQueryable<Product>, IQueryable<Product>> include, CancellationToken ct) =>
                    Task.FromResult(new[] { Product }.FirstOrDefault(predicate.Compile())));

            Mock<IStoreRepository> stores = new(MockBehavior.Strict);
            stores.Setup(repository => repository.GetItemByIdAsync(It.IsAny<Guid>(), It.IsAny<bool>(),
                It.IsAny<Func<IQueryable<Store>, IQueryable<Store>>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Store);

            Mock<IReceiptRepository> receipts = new(MockBehavior.Strict);
            receipts.Setup(repository => repository.GetItemByPredicateAsync(It.IsAny<Expression<Func<Receipt, bool>>>(),
                It.IsAny<bool>(), It.IsAny<Func<IQueryable<Receipt>, IQueryable<Receipt>>>(), It.IsAny<CancellationToken>()))
                .Returns((Expression<Func<Receipt, bool>> predicate, bool tracking,
                    Func<IQueryable<Receipt>, IQueryable<Receipt>> include, CancellationToken ct) =>
                    Task.FromResult(Receipts.FirstOrDefault(predicate.Compile())));

            Mock<IRoleRepository> roles = new(MockBehavior.Strict);
            roles.Setup(repository => repository.GetItemsByPredicateAsync(It.IsAny<Expression<Func<Role, bool>>>(),
                It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<bool>(), It.IsAny<Func<IQueryable<Role>, IQueryable<Role>>>(),
                It.IsAny<CancellationToken>()))
                .Returns((Expression<Func<Role, bool>> predicate, int skip, int? take, bool tracking,
                    Func<IQueryable<Role>, IQueryable<Role>> include, CancellationToken ct) =>
                    Task.FromResult(AvailableRoles.Where(predicate.Compile()).ToList()));

            Users.Setup(repository => repository.GetItemByPredicateAsync(It.IsAny<Expression<Func<User, bool>>>(),
                It.IsAny<bool>(), It.IsAny<Func<IQueryable<User>, IQueryable<User>>>(), It.IsAny<CancellationToken>()))
                .Returns((Expression<Func<User, bool>> predicate, bool tracking,
                    Func<IQueryable<User>, IQueryable<User>> include, CancellationToken ct) =>
                    Task.FromResult(new[] { User }.FirstOrDefault(predicate.Compile())));
            Users.Setup(repository => repository.Create(It.IsAny<User>())).Callback((User user) => CreatedUser = user);
            Hasher.Setup(hasher => hasher.Hash(It.IsAny<string>())).Returns("new-hash");
            Hasher.Setup(hasher => hasher.Verify(It.IsAny<string>(), It.IsAny<string>()))
                .Returns((string password, string hash) => password == "Password1!" && hash == "existing-hash");

            Mock<IAccountMemberRepository> members = new(MockBehavior.Strict);
            members.Setup(repository => repository.GetItemByPredicateAsync(It.IsAny<Expression<Func<AccountMember, bool>>>(),
                It.IsAny<bool>(), It.IsAny<Func<IQueryable<AccountMember>, IQueryable<AccountMember>>>(), It.IsAny<CancellationToken>()))
                .Returns((Expression<Func<AccountMember, bool>> predicate, bool tracking,
                    Func<IQueryable<AccountMember>, IQueryable<AccountMember>> include, CancellationToken ct) =>
                    Task.FromResult(Account.Members.FirstOrDefault(predicate.Compile())));
            members.Setup(repository => repository.GetItemsByPredicateAsync(It.IsAny<Expression<Func<AccountMember, bool>>>(),
                It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<bool>(), It.IsAny<Func<IQueryable<AccountMember>, IQueryable<AccountMember>>>(),
                It.IsAny<CancellationToken>()))
                .Returns((Expression<Func<AccountMember, bool>> predicate, int skip, int? take, bool tracking,
                    Func<IQueryable<AccountMember>, IQueryable<AccountMember>> include, CancellationToken ct) =>
                    Task.FromResult(Account.Members.Where(predicate.Compile()).ToList()));

            Mock<IAccountRepository> accounts = new(MockBehavior.Strict);
            accounts.Setup(repository => repository.GetItemsByPredicateAsync(It.IsAny<Expression<Func<Account, bool>>>(),
                It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<bool>(), It.IsAny<Func<IQueryable<Account>, IQueryable<Account>>>(),
                It.IsAny<CancellationToken>()))
                .Returns((Expression<Func<Account, bool>> predicate, int skip, int? take, bool tracking,
                    Func<IQueryable<Account>, IQueryable<Account>> include, CancellationToken ct) =>
                    Task.FromResult(new[] { Account }.Where(predicate.Compile()).ToList()));

            Work.Setup(work => work.Product).Returns(products.Object);
            Work.Setup(work => work.Store).Returns(stores.Object);
            Work.Setup(work => work.Receipt).Returns(receipts.Object);
            Work.Setup(work => work.Role).Returns(roles.Object);
            Work.Setup(work => work.User).Returns(Users.Object);
            Work.Setup(work => work.AccountMember).Returns(members.Object);
            Work.Setup(work => work.Account).Returns(accounts.Object);
            Work.Setup(work => work.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        }

        public Receipt AddPurchase(Guid authorId)
        {
            Receipt receipt = TestReceiptFactory.Create(createdByUserId: authorId, store: Store);
            Assert.True(receipt.TryAddItem(TestReceiptFactory.CreateItem(Product), out _));
            Receipts.Add(receipt);
            return receipt;
        }

        public async Task<ServiceResult> Rename(bool store, string? name)
        {
            if (store)
            {
                return await TestUseCaseFactory.CreateUpdateStoreAdaptiveNameUseCase(Work.Object)
                    .ExecuteAsync(new() { StoreId = Store.Id, AdaptiveName = name }, User.Id, CancellationToken.None);
            }

            return await TestUseCaseFactory.CreateUpdateProductAdaptiveNameUseCase(Work.Object)
                .ExecuteAsync(new() { ProductId = Product.Id, AdaptiveName = name }, User.Id, CancellationToken.None);
        }

        public void AssertNoWrite()
        {
            Work.Verify(work => work.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
            Work.Verify(work => work.ExecuteInTransaction(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}

