using System.Globalization;
using System.Linq.Expressions;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.Abstractions.Service.Receipts;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.Models.Services.Receipts;
using CostVision.Application.UseCases.Receipts.Accounts;
using CostVision.Application.UseCases.Receipts.Receipts;
using CostVision.Domain.Models.Enums.Receipts;
using CostVision.Domain.Models.Receipts;
using Moq;
using Xunit;
using System.Data;
using CostVision.Application.Extensions;
using CostVision.Application.Abstractions.DataBase.Repositories.MoneyMovements;
using CostVision.Application.Models.Dtos.Receipts;
using CostVision.Application.UseCases.Receipts.Stores;
using CostVision.Domain.Models.MoneyMovements;

namespace CostVision.Application.UnitTests;

public class Stage03RegressionTests
{
    [Theory]
    [InlineData("ru-RU", false)]
    [InlineData("en-US", false)]
    [InlineData("ru-RU", true)]
    [InlineData("en-US", true)]
    public async Task Qr_NewRepeatOther_PreservesBothValidReceipts(string culture, bool withAccount)
    {
        // Arrange
        CultureInfo previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            Fixture fixture = new();
            Guid accountId = Guid.Empty;
            if (withAccount)
            {
                accountId = Guid.NewGuid();
            }

            QrScanRequest request = fixture.QrRequest(accountId, "first", "first", "other");

            // Act
            ServiceResult<AddReceiptScanResponse> result = await fixture.Scan.ExecuteAsync(request, fixture.UserId, CancellationToken.None);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(2, result.Data!.AddedToDbCount);
            Assert.Equal(1, result.Data.ErrorCount);
            Assert.Null(request.Results[0].ErrorMessage);
            Assert.NotNull(request.Results[1].ErrorMessage);
            Assert.Null(request.Results[2].ErrorMessage);
            Assert.Equal(new[] { "first", "other" }, fixture.Created.Select(receipt => receipt.FiscalSign));
            Assert.Equal(withAccount ? 4 : 2, fixture.QueryCount);
            Assert.All(fixture.Created, receipt => Assert.Equal(fixture.UserId, receipt.CreatedByUserId));
            Assert.All(fixture.Created, receipt => Assert.Equal(withAccount ? 1 : 0, receipt.Accounts.Count));
            fixture.Work.Verify(work => work.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Qr_ExistingReceiptRepeat_HasExplicitResultAndNoExtraMutation(bool alreadyLinked)
    {
        // Arrange
        Fixture fixture = new();
        Guid accountId = Guid.NewGuid();
        Receipt existing = fixture.AddPersisted("first", fixture.UserId, alreadyLinked ? accountId : Guid.Empty);
        QrScanRequest request = fixture.QrRequest(accountId, "first", "first", "other");

        // Act
        ServiceResult<AddReceiptScanResponse> result = await fixture.Scan.ExecuteAsync(request, fixture.UserId, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(alreadyLinked ? 1 : 2, result.Data!.AddedToDbCount);
        Assert.Equal(alreadyLinked ? 2 : 1, result.Data.ErrorCount);
        Assert.Single(fixture.Created);
        Assert.Equal("other", fixture.Created[0].FiscalSign);
        Assert.Single(existing.Accounts);
        Assert.Equal(alreadyLinked ? 0 : 1, fixture.Links.Count);
        Assert.NotNull(request.Results[1].ErrorMessage);
        if (!alreadyLinked)
        {
            Assert.True(fixture.TrackedLookupIncludedAccounts);
        }
    }

    [Fact]
    public async Task Qr_ExistingWithoutAccount_IsExplicitRepeatAndDoesNotSave()
    {
        // Arrange
        Fixture fixture = new();
        fixture.AddPersisted("first", fixture.UserId);
        QrScanRequest request = fixture.QrRequest(Guid.Empty, "first", "first");

        // Act
        ServiceResult<AddReceiptScanResponse> result = await fixture.Scan.ExecuteAsync(request, fixture.UserId, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(0, result.Data!.AddedToDbCount);
        Assert.Equal(2, result.Data.ErrorCount);
        Assert.All(request.Results, row => Assert.NotNull(row.ErrorMessage));
        Assert.Empty(fixture.Created);
        Assert.Empty(fixture.Links);
        fixture.Work.Verify(work => work.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Manual_ExistingAuthorCopy_LoadsAccountsBeforeLink()
    {
        // Arrange
        Fixture fixture = new();
        Guid oldAccount = Guid.NewGuid();
        Guid target = Guid.NewGuid();
        Receipt existing = fixture.AddPersisted("first", fixture.UserId, oldAccount);

        // Act
        ServiceResult<AddReceiptManualResponse> result = await fixture.Manual.ExecuteAsync(fixture.ManualRequest(target), fixture.UserId, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(ManualReceiptOutcome.AddedToAccount, result.Data!.Outcome);
        Assert.True(fixture.TrackedLookupIncludedAccounts);
        Assert.Equal(new[] { oldAccount, target }, existing.Accounts.Select(link => link.AccountId));
        Assert.Single(fixture.Links);
        Assert.Empty(fixture.Created);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OtherAuthor_CopyStaysSeparate_AccountDuplicateIsRejected(bool withAccount)
    {
        // Arrange
        Fixture fixture = new();
        Guid account = Guid.NewGuid();
        Receipt other = fixture.AddPersisted("first", Guid.NewGuid(), account);
        Guid target = Guid.Empty;
        if (withAccount)
        {
            target = account;
        }

        // Act
        ServiceResult<AddReceiptManualResponse> result = await fixture.Manual.ExecuteAsync(fixture.ManualRequest(target), fixture.UserId, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(withAccount ? ManualReceiptOutcome.AlreadyExistsInAccount : ManualReceiptOutcome.Created, result.Data!.Outcome);
        Assert.Equal(withAccount ? 0 : 1, fixture.Created.Count);
        Assert.Empty(fixture.Links);
        Assert.Single(other.Accounts);
        if (withAccount)
        {
            fixture.Work.Verify(work => work.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }
    }

    [Theory]
    [InlineData("fn")]
    [InlineData("fd")]
    [InlineData("fp")]
    [InlineData("date")]
    [InlineData("sum")]
    [InlineData("operation")]
    public async Task Manual_EachIdentityFieldDistinguishesDocuments(string changedField)
    {
        // Arrange
        Fixture fixture = new();
        Guid account = Guid.NewGuid();
        fixture.AddPersisted("first", fixture.UserId, account);
        ReceiptManualCreateRequest request = fixture.ManualRequest(account);
        switch (changedField)
        {
            case "fn":
                request.Receipt.FiscalDriveNumber = "other-fn";
                break;
            case "fd":
                request.Receipt.FiscalDocumentNumber = "other-fd";
                break;
            case "fp":
                request.Receipt.FiscalSign = "other-fp";
                break;
            case "date":
                request.Receipt.DateTime = request.Receipt.DateTime.AddMinutes(1);
                break;
            case "sum":
                request.Receipt.Sum += 1m;
                break;
            case "operation":
                request.Receipt.OperationType = ReceiptOperationType.RefundIncome;
                break;
        }

        // Act
        ServiceResult<AddReceiptManualResponse> result = await fixture.Manual.ExecuteAsync(request, fixture.UserId, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(ManualReceiptOutcome.Created, result.Data!.Outcome);
        Assert.Single(fixture.Created);
        Assert.Single(fixture.Created[0].Accounts);
        Assert.Empty(fixture.Links);
    }

    [Fact]
    public void IdentityKey_IsIndependentOfCulture()
    {
        // Arrange
        Receipt receipt = TestReceiptFactory.Create(totalSum: 123.45m);
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
            string russianKey = receipt.GetIdentityKey();
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

            // Act
            string englishKey = receipt.GetIdentityKey();

            // Assert
            Assert.Equal(russianKey, englishKey);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task SaveConflict_IsExplicit_UnknownFailurePropagates_AndRestoresAccounts(bool manual, bool knownConflict)
    {
        // Arrange
        Fixture fixture = new();
        Guid oldAccount = Guid.NewGuid();
        Receipt receipt = fixture.AddPersisted("first", fixture.UserId, oldAccount);
        Exception failure = new InvalidOperationException("storage failure");
        if (knownConflict)
        {
            failure = new ReceiptWriteConflictException("Конкурентный повтор чека.", failure);
        }

        fixture.Work.Setup(work => work.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(failure);
        Guid target = Guid.NewGuid();
        ServiceError? resultError = null;
        bool? success = null;
        bool? hasData = null;
        async Task Execute()
        {
            if (manual)
            {
                ServiceResult<AddReceiptManualResponse> response = await fixture.Manual.ExecuteAsync(fixture.ManualRequest(target), fixture.UserId, CancellationToken.None);
                hasData = response.Data != null;
                resultError = response.Error;
                success = response.Success;
            }
            else
            {
                ServiceResult<AddReceiptScanResponse> response = await fixture.Scan.ExecuteAsync(fixture.QrRequest(target, "first", "other"), fixture.UserId, CancellationToken.None);
                hasData = response.Data != null;
                resultError = response.Error;
                success = response.Success;
            }
        }

        // Act
        Exception? thrown = await Record.ExceptionAsync(Execute);

        // Assert
        if (knownConflict)
        {
            Assert.Null(thrown);
            Assert.False(success);
            Assert.False(hasData);
            Assert.Equal(ServiceErrorType.Conflict, resultError!.Type);
            Assert.Equal(failure.Message, resultError.Message);
        }
        else
        {
            Assert.Same(failure, thrown);
            Assert.Null(success);
            Assert.Null(hasData);
            Assert.Null(resultError);
        }

        Assert.Equal(oldAccount, receipt.Accounts.Single().AccountId);
        Assert.All(fixture.Created, created => Assert.Empty(created.Accounts));
        fixture.Work.Verify(work => work.ExecuteInTransaction(It.IsAny<Func<CancellationToken, Task>>(),
            IsolationLevel.Serializable, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("ru-RU")]
    [InlineData("en-US")]
    public void TypedIdentity_SeparatesDocumentAuthorAndAccount_AndPreservesDecimalEquality(string culture)
    {
        // Arrange
        CultureInfo previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            Guid author = Guid.NewGuid();
            Guid otherAuthor = Guid.NewGuid();
            Guid account = Guid.NewGuid();
            Receipt own = TestReceiptFactory.Create(createdByUserId: author, totalSum: 123.45m);
            Receipt copy = TestReceiptFactory.Create(createdByUserId: otherAuthor, totalSum: 123.4500m);
            Assert.True(copy.TryAddAccount(account, out _, out _));
            ReceiptIdentity identity = own.GetIdentity();

            // Act
            bool matchesDocument = identity.ForDocument().Compile()(copy);
            bool matchesAuthor = identity.ForAuthorCopy(author).Compile()(copy);
            bool matchesAccount = identity.InAccount(account).Compile()(copy);
            bool excluded = identity.InAccount(account, copy.Id).Compile()(copy);

            // Assert
            Assert.Equal(identity, copy.GetIdentity());
            Assert.Equal(identity.GetHashCode(), copy.GetIdentity().GetHashCode());
            Assert.Equal(own.GetIdentityKey(), copy.GetIdentityKey());
            Assert.True(matchesDocument);
            Assert.False(matchesAuthor);
            Assert.True(matchesAccount);
            Assert.False(excluded);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AccountDuplicate_RefusesBeforeAnyWrite(bool qr)
    {
        // Arrange
        Fixture fixture = new();
        Guid account = Guid.NewGuid();
        Receipt other = fixture.AddPersisted("first", Guid.NewGuid(), account);
        Receipt own = fixture.AddPersisted("first", fixture.UserId);
        ServiceResult<AddReceiptScanResponse>? scanResult = null;
        ServiceResult<AddReceiptManualResponse>? manualResult = null;

        // Act
        if (qr)
        {
            scanResult = await fixture.Scan.ExecuteAsync(fixture.QrRequest(account, "first"), fixture.UserId, CancellationToken.None);
        }
        else
        {
            manualResult = await fixture.Manual.ExecuteAsync(fixture.ManualRequest(account), fixture.UserId, CancellationToken.None);
        }

        // Assert
        if (qr)
        {
            Assert.True(scanResult!.Success);
            Assert.Equal(1, scanResult.Data!.ErrorCount);
            Assert.Equal(0, scanResult.Data.AddedToDbCount);
        }
        else
        {
            Assert.True(manualResult!.Success);
            Assert.Equal(ManualReceiptOutcome.AlreadyExistsInAccount, manualResult.Data!.Outcome);
        }

        Assert.Empty(own.Accounts);
        Assert.Single(other.Accounts);
        Assert.Empty(fixture.Created);
        Assert.Empty(fixture.Links);
        fixture.Work.Verify(work => work.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("ru-RU", false)]
    [InlineData("en-US", false)]
    [InlineData("ru-RU", true)]
    [InlineData("en-US", true)]
    public async Task Reports_GroupTypedDocuments_WithoutCultureScaleOrDelimiterCollisions(string culture, bool storeReport)
    {
        // Arrange
        Fixture fixture = new();
        Account account = fixture.AddAccount();
        Store store = TestReceiptFactory.CreateStore("Store");
        store.Id = Guid.NewGuid();
        fixture.AddPersisted("first", fixture.UserId, account.Id, store: store);
        fixture.AddPersisted("first", Guid.NewGuid(), account.Id, sum: 123.4500m, store: store);
        fixture.AddPersisted("first", fixture.UserId, fn: "a|b", fd: "c", store: store);
        fixture.AddPersisted("first", fixture.UserId, fn: "a", fd: "b|c", store: store);
        CultureInfo previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            // Act
            List<ReceiptDto>? rows;
            bool success;
            if (storeReport)
            {
                ServiceResult<StoreReceiptListDto> result = await new GetStoreReceiptListUseCase(fixture.Work.Object)
                    .ExecuteAsync(new GetStoreReceiptListRequest { StoreId = store.Id, PageSize = 10 }, fixture.UserId, CancellationToken.None);
                success = result.Success;
                rows = result.Data?.Items;
            }
            else
            {
                ServiceResult<List<ReceiptDto>> result = await new GetReceiptListUseCase(fixture.Work.Object)
                    .ExecuteAsync(TestUserFactory.Create(fixture.UserId), new DateTime(2026, 1, 1), new DateTime(2026, 1, 1), CancellationToken.None);
                success = result.Success;
                rows = result.Data;
            }

            // Assert
            Assert.True(success);
            Assert.NotNull(rows);
            Assert.Equal(3, rows.Count);
            Assert.All(rows, row => Assert.Equal(123.45m, row.TotalSum));
            Assert.All(rows, row => Assert.Equal(0m, row.MoneyMovementsTotalSum));
            Assert.Single(rows.Single(row => row.FiscalDriveNumber == "fn").Accounts);
            Assert.Contains(rows, row => row.FiscalDriveNumber == "a|b" && row.FiscalDocumentNumber == "c");
            Assert.Contains(rows, row => row.FiscalDriveNumber == "a" && row.FiscalDocumentNumber == "b|c");
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Transfer_UsesDocumentIdentityAcrossAuthors_AndRefusesWithoutMutating(bool duplicate)
    {
        // Arrange
        Fixture fixture = new();
        Account source = fixture.AddAccount();
        Account target = fixture.AddAccount();
        Receipt own = fixture.AddPersisted("first", fixture.UserId, source.Id);
        fixture.AddPersisted(duplicate ? "first" : "other", Guid.NewGuid(), target.Id);

        // Act
        ServiceResult result = await new MoveReceiptToAccountUseCase(fixture.Work.Object)
            .ExecuteAsync(source.Id, target.Id, own.Id, fixture.UserId, CancellationToken.None);

        // Assert
        Assert.Equal(!duplicate, result.Success);
        Assert.Equal(duplicate ? source.Id : target.Id, own.Accounts.Single().AccountId);
        Assert.Equal(duplicate ? 0 : 1, fixture.Links.Count);
        Assert.Equal(duplicate ? 0 : 1, fixture.DeletedLinks.Count);
        if (duplicate)
        {
            Assert.Equal(ServiceErrorType.Conflict, result.Error!.Type);
            fixture.Work.Verify(work => work.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }
    }

    [Fact]
    public async Task Transfer_SaveConflictRestoresSourceAccount()
    {
        // Arrange
        Fixture fixture = new();
        Account source = fixture.AddAccount();
        Account target = fixture.AddAccount();
        Receipt own = fixture.AddPersisted("first", fixture.UserId, source.Id);
        fixture.Work.Setup(work => work.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ReceiptWriteConflictException("Conflict", new InvalidOperationException()));

        // Act
        ServiceResult result = await new MoveReceiptToAccountUseCase(fixture.Work.Object)
            .ExecuteAsync(source.Id, target.Id, own.Id, fixture.UserId, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.Conflict, result.Error!.Type);
        Assert.Equal(source.Id, own.Accounts.Single().AccountId);
        Assert.Same(source, own.Accounts.Single().Account);
    }

    private class Fixture
    {
        public Guid UserId { get; } = Guid.NewGuid();
        public Mock<IUnitOfWork> Work { get; } = new(MockBehavior.Strict);
        public List<Receipt> Created { get; } = [];
        public List<ReceiptAccount> Links { get; } = [];
        public List<ReceiptAccount> DeletedLinks { get; } = [];
        public bool TrackedLookupIncludedAccounts { get; private set; }
        public int QueryCount { get; private set; }
        public SaveReceiptsScannedUseCase Scan { get; }
        public SaveManualReceiptUseCase Manual { get; }
        private readonly List<Receipt> _persisted = [];
        private readonly Dictionary<Guid, Receipt> _tracked = [];
        private readonly List<Account> _accounts = [];
        private bool _insideTransaction;

        public Fixture()
        {
            Work.Setup(work => work.ExecuteInTransaction(It.IsAny<Func<CancellationToken, Task>>(),
                    IsolationLevel.Serializable, It.IsAny<CancellationToken>()))
                .Returns((Func<CancellationToken, Task> action, IsolationLevel isolation, CancellationToken ct) => RunTransactionAsync(action, ct));

            Mock<IReceiptRepository> receipts = new(MockBehavior.Strict);
            receipts.Setup(repository => repository.GetItemByPredicateAsync(
                It.IsAny<Expression<Func<Receipt, bool>>>(), It.IsAny<bool>(),
                It.IsAny<Func<IQueryable<Receipt>, IQueryable<Receipt>>?>(), It.IsAny<CancellationToken>()))
                .Returns((Expression<Func<Receipt, bool>> predicate, bool noTracking,
                    Func<IQueryable<Receipt>, IQueryable<Receipt>>? include, CancellationToken ct) =>
                {
                    QueryCount++;
                    if (predicate.Body.ToString().Contains("FiscalDriveNumber", StringComparison.Ordinal))
                    {
                        Assert.True(_insideTransaction);
                    }

                    Receipt? match = _persisted.FirstOrDefault(predicate.Compile());
                    if (!noTracking)
                    {
                        TrackedLookupIncludedAccounts = include != null;
                    }

                    if (match == null)
                    {
                        return Task.FromResult<Receipt?>(null);
                    }

                    return Task.FromResult<Receipt?>(_tracked[match.Id]);
                });
            receipts.Setup(repository => repository.Create(It.IsAny<Receipt>())).Callback<Receipt>(Created.Add);
            receipts.Setup(repository => repository.GetItemsByPredicateAsync(
                It.IsAny<Expression<Func<Receipt, bool>>>(), It.IsAny<int>(), It.IsAny<int?>(),
                It.IsAny<bool>(), It.IsAny<Func<IQueryable<Receipt>, IQueryable<Receipt>>?>(), It.IsAny<CancellationToken>()))
                .Returns((Expression<Func<Receipt, bool>> predicate, int skip, int? take, bool tracking,
                    Func<IQueryable<Receipt>, IQueryable<Receipt>>? include, CancellationToken ct) =>
                    Task.FromResult(_persisted.Where(predicate.Compile()).Select(receipt => _tracked[receipt.Id]).ToList()));
            Mock<IReceiptAccountRepository> links = new(MockBehavior.Strict);
            links.Setup(repository => repository.Create(It.IsAny<ReceiptAccount>())).Callback<ReceiptAccount>(Links.Add);
            links.Setup(repository => repository.Delete(It.IsAny<ReceiptAccount>())).Callback<ReceiptAccount>(DeletedLinks.Add);
            Mock<IAccountRepository> accounts = new(MockBehavior.Strict);
            accounts.Setup(repository => repository.GetItemByPredicateAsync(
                It.IsAny<Expression<Func<Account, bool>>>(), It.IsAny<bool>(),
                It.IsAny<Func<IQueryable<Account>, IQueryable<Account>>?>(), It.IsAny<CancellationToken>()))
                .Returns((Expression<Func<Account, bool>> predicate, bool tracking,
                    Func<IQueryable<Account>, IQueryable<Account>>? include, CancellationToken ct) =>
                    Task.FromResult(_accounts.FirstOrDefault(predicate.Compile())));
            Mock<IMoneyMovementRepository> movements = new(MockBehavior.Strict);
            movements.Setup(repository => repository.GetItemsByPredicateAsync(
                It.IsAny<Expression<Func<MoneyMovement, bool>>>(), It.IsAny<int>(), It.IsAny<int?>(),
                It.IsAny<bool>(), It.IsAny<Func<IQueryable<MoneyMovement>, IQueryable<MoneyMovement>>?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);
            Work.Setup(work => work.Receipt).Returns(receipts.Object);
            Work.Setup(work => work.ReceiptAccount).Returns(links.Object);
            Work.Setup(work => work.Account).Returns(accounts.Object);
            Work.Setup(work => work.MoneyMovement).Returns(movements.Object);
            Work.Setup(work => work.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Callback(() => Assert.True(_insideTransaction)).Returns(Task.CompletedTask);
            Work.Setup(work => work.ExecuteInTransaction(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
                .Returns((Func<CancellationToken, Task> action, CancellationToken ct) => action(ct));
            Mock<IValidateReceiptCreationAccessUseCase> access = new(MockBehavior.Strict);
            access.Setup(useCase => useCase.ExecuteAsync(It.IsAny<Guid>(), UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(ServiceResult.Ok());
            Mock<IQrParser> parser = new(MockBehavior.Strict);
            parser.Setup(item => item.Parse(It.IsAny<string>())).Returns((string key) => new QrParsed
            {
                FiscalDriveNumber = "fn", FiscalDocumentNumber = "fd", FiscalSign = key,
                DateTime = new DateTime(2026, 1, 1), Sum = 123.45m, OperationType = (int)ReceiptOperationType.Income
            });
            Scan = new(Work.Object, access.Object, parser.Object);
            Manual = new(Work.Object, access.Object);
        }

        private async Task RunTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct)
        {
            _insideTransaction = true;
            try
            {
                await action(ct);
            }
            finally
            {
                _insideTransaction = false;
            }
        }

        public Receipt AddPersisted(string sign, Guid author, Guid account = default,
            decimal sum = 123.45m, string fn = "fn", string fd = "fd", Store? store = null)
        {
            Receipt tracked = TestReceiptFactory.Create(createdByUserId: author, totalSum: sum,
                fiscalSign: sign, fiscalDriveNumber: fn, fiscalDocumentNumber: fd, store: store);
            Receipt snapshot = TestReceiptFactory.Create(tracked.Id, author, totalSum: sum,
                fiscalSign: sign, fiscalDriveNumber: fn, fiscalDocumentNumber: fd, store: store);
            if (account != Guid.Empty)
            {
                Assert.True(tracked.TryAddAccount(account, out _, out _));
                Assert.True(snapshot.TryAddAccount(account, out _, out _));
                Account? model = _accounts.FirstOrDefault(item => item.Id == account);
                if (model != null)
                {
                    tracked.Accounts.Single().AttachAccount(model);
                    snapshot.Accounts.Single().AttachAccount(model);
                }
            }

            _persisted.Add(snapshot);
            _tracked.Add(tracked.Id, tracked);
            return tracked;
        }

        public Account AddAccount()
        {
            Account account = TestAccountFactory.Create(ownerUserId: UserId);
            _accounts.Add(account);
            return account;
        }

        public QrScanRequest QrRequest(Guid account, params string[] keys)
        {
            return new QrScanRequest { AccountId = account, Results = keys.Select(key => new QrScanResult { DecodedText = key }).ToList() };
        }

        public ReceiptManualCreateRequest ManualRequest(Guid account)
        {
            return new ReceiptManualCreateRequest
            {
                AccountId = account,
                Receipt = new ManualReceiptInput
                {
                    FiscalDriveNumber = "fn", FiscalDocumentNumber = "fd", FiscalSign = "first",
                    DateTime = new DateTime(2026, 1, 1), Sum = 123.45m, OperationType = ReceiptOperationType.Income
                }
            };
        }
    }
}
