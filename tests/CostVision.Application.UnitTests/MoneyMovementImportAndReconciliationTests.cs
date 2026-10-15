using CostVision.Tests;
using System.Data;
using System.Globalization;
using System.Linq.Expressions;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.MoneyMovements;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.Models.Requests.MoneyMovements;
using CostVision.Application.Models.Dtos.MoneyMovements;
using CostVision.Application.UseCases.MoneyMovements.BankStatementImports.Parsing;
using CostVision.Application.UnitTests.UseCases.MoneyMovements;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.MoneyMovements;
using CostVision.Application.UseCases.Receipts.Accounts;
using CostVision.Domain.Models.Enums.Authorization;
using CostVision.Domain.Models.Enums.MoneyMovements;
using CostVision.Domain.Models.Enums.Receipts;
using CostVision.Domain.Models.MoneyMovements;
using CostVision.Domain.Models.Receipts;
using Moq;
using Xunit;

namespace CostVision.Application.UnitTests;

public class MoneyMovementImportAndReconciliationTests : MoneyMovementUseCaseTestBase
{
    [Theory]
    [InlineData("ru-RU", true, 1, 0)]
    [InlineData("en-US", true, 1, 0)]
    [InlineData("ru-RU", false, 0, 1)]
    [InlineData("en-US", false, 0, 1)]
    public async Task Import_TracksPendingRowsAndCounts(string culture, bool replace, int updated, int errors)
    {
        // Arrange
        CultureInfo previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            Fixture fixture = new();
            BankStatementImportRowRequest repeated = fixture.Row(comment: "replacement");
            repeated.ReplaceDuplicate = replace;
            SaveBankStatementImportRequest request = new()
            {
                AccountId = fixture.Account.Id,
                Rows = [fixture.Row(), repeated, fixture.Row(amount: 200, importComment: "other")]
            };

            // Act
            ServiceResult<BankStatementImportResultDto> result = await TestUseCaseFactory.CreateImportMoneyMovementsUseCase(fixture.Work.Object)
                .ExecuteAsync(request, fixture.UserId, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(2, result.Data!.CreatedCount);
            Assert.Equal(updated, result.Data.UpdatedCount);
            Assert.Equal(errors, result.Data.ErrorCount);
            Assert.Equal(2, fixture.Created.Count);
            Assert.Equal(2, fixture.Saved.Count);
            if (replace)
            {
                Assert.Equal("replacement", fixture.Created[0].Comment);
            }
            else
            {
                Assert.Equal(2, Assert.Single(result.Data.Errors).LineNumber);
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData("foreign")]
    [InlineData("stale")]
    [InlineData("manual")]
    [InlineData("amount")]
    [InlineData("time")]
    [InlineData("type")]
    [InlineData("text")]
    public async Task Import_RejectsWrongDuplicateIdWithoutMutation(string mismatch)
    {
        // Arrange
        Fixture fixture = new();
        MoneyMovement existing = fixture.Imported();
        fixture.Saved.Add(existing);
        BankStatementImportRowRequest row = fixture.Row(comment: "tampered");
        row.DuplicateMoneyMovementId = existing.Id;
        row.ReplaceDuplicate = true;
        switch (mismatch)
        {
            case "foreign":
                existing.TryMoveToAccount(Guid.NewGuid(), out _);
                break;
            case "stale":
                row.DuplicateMoneyMovementId = Guid.NewGuid();
                break;
            case "manual":
                fixture.Saved.Clear();
                fixture.Saved.Add(TestMoneyMovementFactory.CreateManual(existing.Id, fixture.Account.Id,
                    100, fixture.Date, fixture.UserId, type: MoneyMovementType.Expense));
                break;
            case "amount":
                row.Amount++;
                break;
            case "time":
                row.OccurredAt = row.OccurredAt.AddSeconds(1);
                break;
            case "type":
                row.Type = MoneyMovementType.Income;
                break;
            case "text":
                row.ImportComment = "different";
                break;
        }

        // Act
        ServiceResult<BankStatementImportResultDto> result = await fixture.Import(row);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(0, result.Data!.CreatedCount);
        Assert.Equal(0, result.Data.UpdatedCount);
        Assert.Equal(1, result.Data.ErrorCount);
        Assert.Equal(1, Assert.Single(result.Data.Errors).LineNumber);
        Assert.Equal("old", existing.Comment);
        Assert.Null(existing.UpdatedAtUtc);
        Assert.Empty(fixture.Created);
        Assert.Equal(0, fixture.SaveCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Import_ExistingRepeatOnlyChangesComment(bool includeId)
    {
        // Arrange
        Fixture fixture = new();
        MoneyMovement existing = fixture.Imported();
        Receipt receipt = fixture.Receipt();
        MoneyMovementReceipt link = TestMoneyMovementFactory.CreateLink(existing, receipt);
        fixture.Saved.Add(existing);
        BankStatementImportRowRequest row = fixture.Row(comment: "new");
        row.ReplaceDuplicate = true;
        if (includeId)
        {
            row.DuplicateMoneyMovementId = existing.Id;
        }

        // Act
        ServiceResult<BankStatementImportResultDto> result = await fixture.Import(row, row);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(0, result.Data!.CreatedCount);
        Assert.Equal(2, result.Data.UpdatedCount);
        Assert.Equal(0, result.Data.ErrorCount);
        Assert.Equal("new", existing.Comment);
        Assert.Equal(fixture.UserId, existing.CreatedByUserId);
        Assert.Equal(fixture.UserId, existing.PerformedByUserId);
        Assert.Same(link, Assert.Single(existing.ReceiptLinks));
        Assert.NotNull(existing.UpdatedAtUtc);
        Assert.Empty(fixture.Created);
    }

    [Theory]
    [InlineData(AccountAccessRole.Owner, true)]
    [InlineData(AccountAccessRole.Editor, true)]
    [InlineData(AccountAccessRole.Viewer, false)]
    public async Task Import_EnforcesMutationRole(AccountAccessRole role, bool allowed)
    {
        // Arrange
        Fixture fixture = new(role);

        // Act
        ServiceResult<BankStatementImportResultDto> result = await fixture.Import(fixture.Row());

        // Assert
        Assert.Equal(allowed, result.Success);
        Assert.Equal(allowed ? 1 : 0, fixture.Created.Count);
        if (!allowed)
        {
            Assert.Equal(ServiceErrorType.Forbidden, result.Error!.Type);
            Assert.Equal(0, fixture.SaveCount);
        }
    }

    [Theory]
    [InlineData(AccountAccessRole.Owner, 1)]
    [InlineData(AccountAccessRole.Editor, 1)]
    [InlineData(AccountAccessRole.Viewer, 0)]
    public async Task AutoLink_EnforcesMutationRole(AccountAccessRole role, int expected)
    {
        // Arrange
        Fixture fixture = new(role);
        fixture.Saved.Add(fixture.Manual());
        fixture.Receipts.Add(fixture.Receipt());

        // Act
        ServiceResult<int> result = await TestUseCaseFactory.CreateAutoLinkExactMoneyMovementReceiptsUseCase(fixture.Work.Object)
            .ExecuteAsync(fixture.UserId, fixture.Date, fixture.Date, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(expected, result.Data);
        Assert.Equal(expected, fixture.Links.Count);
        Assert.Equal(expected, fixture.SaveCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MoveMovement_RejectsIncompatibleLinksBeforeMutation(bool compatible)
    {
        // Arrange
        Fixture fixture = new();
        MoneyMovement movement = fixture.Manual();
        Receipt receipt = fixture.Receipt();
        if (compatible)
        {
            Assert.True(receipt.TryAddAccount(fixture.Target, out _, out _));
        }
        MoneyMovementReceipt link = TestMoneyMovementFactory.CreateLink(movement, receipt);
        fixture.Saved.Add(movement);
        fixture.Receipts.Add(receipt);

        // Act
        ServiceResult result = await TestUseCaseFactory.CreateMoveMoneyMovementToAccountUseCase(fixture.Work.Object).ExecuteAsync(new()
        {
            MoneyMovementId = movement.Id,
            SourceAccountId = fixture.Account.Id,
            TargetAccountId = fixture.Target.Id
        }, fixture.UserId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(compatible, result.Success);
        Assert.Equal(compatible ? fixture.Target.Id : fixture.Account.Id, movement.AccountId);
        Assert.Same(link, Assert.Single(movement.ReceiptLinks));
        Assert.Equal(compatible ? 1 : 0, fixture.SaveCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MoveReceipt_RejectsIncompatibleLinksBeforeMutation(bool compatible)
    {
        // Arrange
        Fixture fixture = new();
        Receipt receipt = fixture.Receipt();
        MoneyMovement movement = fixture.Manual();
        if (compatible)
        {
            Assert.True(movement.TryMoveToAccount(fixture.Target, out _));
        }
        MoneyMovementReceipt link = TestMoneyMovementFactory.CreateLink(movement, receipt);
        fixture.Saved.Add(movement);
        fixture.Receipts.Add(receipt);

        // Act
        ServiceResult result = await TestUseCaseFactory.CreateMoveReceiptToAccountUseCase(fixture.Work.Object)
            .ExecuteAsync(fixture.Account.Id, fixture.Target.Id, receipt.Id, fixture.UserId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(compatible, result.Success);
        Assert.Equal(compatible ? fixture.Target.Id : fixture.Account.Id, Assert.Single(receipt.Accounts).AccountId);
        Assert.Same(link, Assert.Single(receipt.MoneyMovementLinks));
        Assert.Equal(compatible ? 1 : 0, fixture.SaveCount);
        Assert.Equal(compatible ? 1 : 0, fixture.AccountCreates);
        Assert.Equal(compatible ? 1 : 0, fixture.AccountDeletes);
    }

    [Fact]
    public async Task Import_InvalidHintDoesNotReserveKeyOrLoseValidNeighbour()
    {
        // Arrange
        Fixture fixture = new();
        BankStatementImportRowRequest invalid = fixture.Row();
        invalid.ReplaceDuplicate = true;
        invalid.DuplicateMoneyMovementId = Guid.NewGuid();

        // Act
        ServiceResult<BankStatementImportResultDto> result = await fixture.Import(invalid, fixture.Row(), fixture.Row(amount: 200));

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.Data!.CreatedCount);
        Assert.Equal(0, result.Data.UpdatedCount);
        Assert.Equal(1, result.Data.ErrorCount);
        Assert.Equal(1, Assert.Single(result.Data.Errors).LineNumber);
        Assert.Equal(2, fixture.Saved.Count);
    }

    [Fact]
    public async Task Import_AmbiguousStoredKeyDoesNotReplaceArbitraryOperation()
    {
        // Arrange
        Fixture fixture = new();
        fixture.Saved.AddRange([fixture.Imported(), fixture.Imported()]);
        BankStatementImportRowRequest row = fixture.Row(comment: "new");
        row.ReplaceDuplicate = true;
        row.DuplicateMoneyMovementId = fixture.Saved[0].Id;

        // Act
        ServiceResult<BankStatementImportResultDto> result = await fixture.Import(row);

        // Assert
        Assert.Equal(1, result.Data!.ErrorCount);
        Assert.Equal(0, result.Data.UpdatedCount);
        Assert.All(fixture.Saved, movement => Assert.Equal("old", movement.Comment));
        Assert.Equal(0, fixture.SaveCount);
    }

    [Theory]
    [InlineData("ru-RU", "en-US")]
    [InlineData("en-US", "ru-RU")]
    public async Task PreviewAndImport_ShareNormalizedKeyAcrossCultures(string previewCulture, string saveCulture)
    {
        // Arrange
        Fixture fixture = new();
        MoneyMovement existing = fixture.Imported(123.45m);
        fixture.Saved.Add(existing);
        BankStatementImportPreviewDto preview = new()
        {
            AccountId = fixture.Account.Id,
            Rows = [CreatePreviewRow(fixture.Date, 123.45m, " purchase "), CreatePreviewRow(fixture.Date, 123.450m, "purchase")]
        };
        Mock<IBankStatementParser> parser = CreateParser("test", preview);
        PreviewBankStatementImportUseCase useCase = TestUseCaseFactory.CreatePreviewBankStatementImportUseCase(fixture.Work.Object,
            CreateParserRegistry(parser.Object).Object, CreatePdfTextExtractor(["text"]).Object);
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(previewCulture);

            // Act
            ServiceResult<BankStatementImportPreviewDto> previewResult = await useCase.ExecuteAsync(new()
            {
                BankId = "test", AccountId = fixture.Account.Id, FileName = "test.pdf",
                FileStream = new MemoryStream([1]), FileLength = 1
            }, fixture.UserId, TestContext.Current.CancellationToken);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(saveCulture);
            BankStatementImportRowRequest row = fixture.Row(amount: 123.4500m, importComment: " purchase ");
            row.ReplaceDuplicate = true;
            row.DuplicateMoneyMovementId = previewResult.Data!.Rows[0].DuplicateMoneyMovementId;
            ServiceResult<BankStatementImportResultDto> result = await fixture.Import(row);

            // Assert
            Assert.All(previewResult.Data.Rows, item => Assert.True(item.IsDuplicate));
            Assert.Equal(existing.Id, row.DuplicateMoneyMovementId);
            Assert.Equal(0, result.Data!.CreatedCount);
            Assert.Equal(1, result.Data.UpdatedCount);
            Assert.Equal(0, result.Data.ErrorCount);
            Assert.Equal("purchase", existing.ImportComment);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData(100.001)]
    [InlineData(10000000000000000)]
    public async Task Import_RejectsAmountsThatStorageWouldRoundOrOverflow(decimal amount)
    {
        // Arrange
        Fixture fixture = new();

        // Act
        ServiceResult<BankStatementImportResultDto> result = await fixture.Import(fixture.Row(amount));

        // Assert
        Assert.Equal(1, result.Data!.ErrorCount);
        Assert.Empty(fixture.Created);
        Assert.Equal(0, fixture.SaveCount);
    }

    [Theory]
    [InlineData("receipts")]
    [InlineData("movements")]
    [InlineData("accounts")]
    [InlineData("linked")]
    public async Task AutoLink_SkipsAmbiguityAndOccupiedObjects(string scenario)
    {
        // Arrange
        Fixture fixture = new();
        MoneyMovement movement = fixture.Manual();
        Receipt receipt = fixture.Receipt();
        fixture.Saved.Add(movement);
        fixture.Receipts.Add(receipt);
        switch (scenario)
        {
            case "receipts":
                fixture.Receipts.Add(fixture.Receipt());
                break;
            case "movements":
                fixture.Saved.Add(fixture.Manual());
                break;
            case "accounts":
                Assert.True(receipt.TryAddAccount(fixture.Target, out _, out _));
                MoneyMovement other = fixture.Manual();
                Assert.True(other.TryMoveToAccount(fixture.Target, out _));
                fixture.Saved.Add(other);
                break;
            case "linked":
                TestMoneyMovementFactory.CreateLink(movement, receipt);
                break;
        }

        // Act
        ServiceResult<int> result = await TestUseCaseFactory.CreateAutoLinkExactMoneyMovementReceiptsUseCase(fixture.Work.Object)
            .ExecuteAsync(fixture.UserId, fixture.Date, fixture.Date, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(0, result.Data);
        Assert.Empty(fixture.Links);
        Assert.Equal(0, fixture.SaveCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CreateAndBatch_UseSameExactMatchingRule(bool ambiguous)
    {
        // Arrange
        Fixture fixture = new();
        Receipt receipt = fixture.Receipt();
        fixture.Receipts.Add(receipt);
        if (ambiguous)
        {
            fixture.Saved.Add(fixture.Manual());
        }

        // Act
        ServiceResult<MoneyMovementDto> result = await TestUseCaseFactory.CreateCreateMoneyMovementUseCase(fixture.Work.Object).ExecuteAsync(new()
        {
            AccountId = fixture.Account.Id, Amount = 100, Type = MoneyMovementType.Expense, OccurredAt = fixture.Date
        }, fixture.UserId, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Success);
        Assert.Single(fixture.Created);
        Assert.Equal(ambiguous ? 0 : 1, fixture.Links.Count);
        if (!ambiguous)
        {
            Assert.Equal(receipt.Id, fixture.Links[0].ReceiptId);
            Assert.Equal(fixture.Created[0].Id, fixture.Links[0].MoneyMovementId);
        }
    }

    [Fact]
    public async Task ManualLink_PreservesAllowedMultipleLinks()
    {
        // Arrange
        Fixture fixture = new();
        MoneyMovement movement = fixture.Manual();
        fixture.Saved.Add(movement);
        Receipt first = fixture.Receipt();
        Receipt second = fixture.Receipt();
        fixture.Receipts.AddRange([first, second]);
        TestMoneyMovementFactory.CreateLink(movement, first);

        // Act
        ServiceResult result = await TestUseCaseFactory.CreateLinkMoneyMovementReceiptUseCase(fixture.Work.Object).ExecuteAsync(new()
        {
            MoneyMovementId = movement.Id, ReceiptId = second.Id
        }, fixture.UserId, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(second.Id, Assert.Single(fixture.Links).ReceiptId);
        Assert.Equal(1, fixture.SaveCount);
        Assert.Equal(first.Id, Assert.Single(movement.ReceiptLinks).ReceiptId);
    }

    [Fact]
    public async Task MoveReceipt_KeepsOtherCompatibleAccountAndAllLinks()
    {
        // Arrange
        Fixture fixture = new();
        Receipt receipt = fixture.Receipt();
        Account other = TestAccountFactory.Create(ownerUserId: Guid.NewGuid());
        Assert.True(receipt.TryAddAccount(other, out _, out _));
        MoneyMovement movement = fixture.Manual();
        Assert.True(movement.TryMoveToAccount(other, out _));
        MoneyMovementReceipt link = TestMoneyMovementFactory.CreateLink(movement, receipt);
        fixture.Receipts.Add(receipt);

        // Act
        ServiceResult result = await TestUseCaseFactory.CreateMoveReceiptToAccountUseCase(fixture.Work.Object).ExecuteAsync(fixture.Account.Id,
            fixture.Target.Id, receipt.Id, fixture.UserId, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Success);
        Assert.Contains(receipt.Accounts, account => account.AccountId == fixture.Target.Id);
        Assert.Contains(receipt.Accounts, account => account.AccountId == other.Id);
        Assert.Same(link, Assert.Single(receipt.MoneyMovementLinks));
        Assert.Equal(other.Id, movement.AccountId);
    }

    [Fact]
    public async Task MoveImportedMovement_RejectsExistingTargetKey()
    {
        // Arrange
        Fixture fixture = new();
        MoneyMovement movement = fixture.Imported();
        MoneyMovement duplicate = fixture.Imported();
        Assert.True(duplicate.TryMoveToAccount(fixture.Target, out _));
        fixture.Saved.AddRange([movement, duplicate]);

        // Act
        ServiceResult result = await TestUseCaseFactory.CreateMoveMoneyMovementToAccountUseCase(fixture.Work.Object).ExecuteAsync(new()
        {
            MoneyMovementId = movement.Id, SourceAccountId = fixture.Account.Id, TargetAccountId = fixture.Target.Id
        }, fixture.UserId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ServiceErrorType.Conflict, result.Error!.Type);
        Assert.Equal(fixture.Account.Id, movement.AccountId);
        Assert.Equal(0, fixture.SaveCount);
    }

    [Theory]
    [InlineData(AccountAccessRole.Editor, true)]
    [InlineData(AccountAccessRole.Viewer, false)]
    public async Task Transfers_RequireMutationRoleAndDoNotPartiallyChangeObjects(AccountAccessRole role, bool allowed)
    {
        // Arrange
        Fixture fixture = new(role);
        MoneyMovement movement = fixture.Manual();
        Receipt receipt = fixture.Receipt();
        fixture.Saved.Add(movement);
        fixture.Receipts.Add(receipt);

        // Act
        ServiceResult moneyResult = await TestUseCaseFactory.CreateMoveMoneyMovementToAccountUseCase(fixture.Work.Object).ExecuteAsync(new()
        {
            MoneyMovementId = movement.Id, SourceAccountId = fixture.Account.Id, TargetAccountId = fixture.Target.Id
        }, fixture.UserId, TestContext.Current.CancellationToken);
        ServiceResult receiptResult = await TestUseCaseFactory.CreateMoveReceiptToAccountUseCase(fixture.Work.Object).ExecuteAsync(
            fixture.Account.Id, fixture.Target.Id, receipt.Id, fixture.UserId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(allowed, moneyResult.Success);
        Assert.Equal(allowed, receiptResult.Success);
        if (!allowed)
        {
            Assert.Equal(ServiceErrorType.Forbidden, moneyResult.Error!.Type);
            Assert.Equal(ServiceErrorType.Forbidden, receiptResult.Error!.Type);
            Assert.Equal(fixture.Account.Id, movement.AccountId);
            Assert.Equal(fixture.Account.Id, Assert.Single(receipt.Accounts).AccountId);
            Assert.Equal(0, fixture.SaveCount);
            Assert.Equal(0, fixture.AccountCreates);
            Assert.Equal(0, fixture.AccountDeletes);
        }
    }

    [Theory]
    [InlineData("time")]
    [InlineData("amount")]
    [InlineData("operation")]
    [InlineData("account")]
    public async Task AutoLink_SkipsInexactCandidates(string mismatch)
    {
        // Arrange
        Fixture fixture = new();
        fixture.Saved.Add(fixture.Manual());
        DateTime date = fixture.Date;
        decimal amount = 100;
        ReceiptOperationType operation = ReceiptOperationType.Income;
        if (mismatch == "time")
        {
            date = date.AddDays(1);
        }
        if (mismatch == "amount")
        {
            amount++;
        }
        if (mismatch == "operation")
        {
            operation = ReceiptOperationType.RefundIncome;
        }
        Receipt receipt = TestReceiptFactory.Create(createdByUserId: fixture.UserId, dateTime: date,
            totalSum: amount, operationType: operation);
        Account account = fixture.Account;
        if (mismatch == "account")
        {
            account = fixture.Target;
        }
        Assert.True(receipt.TryAddAccount(account, out _, out _));
        fixture.Receipts.Add(receipt);

        // Act
        ServiceResult<int> result = await TestUseCaseFactory.CreateAutoLinkExactMoneyMovementReceiptsUseCase(fixture.Work.Object)
            .ExecuteAsync(fixture.UserId, fixture.Date, fixture.Date, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, result.Data);
        Assert.Empty(fixture.Links);
        Assert.Equal(0, fixture.SaveCount);
    }

    [Fact]
    public void ReplaceDuplicate_DomainRejectsManualConversionAndMismatchedIdentity()
    {
        // Arrange
        Fixture fixture = new();
        MoneyMovement imported = fixture.Imported();
        MoneyMovement manual = fixture.Manual();

        // Act
        bool importedResult = imported.TryReplaceFromBankStatement(200, imported.Type, imported.OccurredAt,
            "changed", imported.ImportComment, DateTime.UtcNow, out _);
        bool manualResult = manual.TryReplaceFromBankStatement(manual.Amount, manual.Type, manual.OccurredAt,
            "changed", "purchase", DateTime.UtcNow, out _);

        // Assert
        Assert.False(importedResult);
        Assert.False(manualResult);
        Assert.Equal(100, imported.Amount);
        Assert.Equal("old", imported.Comment);
        Assert.Equal(MoneyMovementSource.Manual, manual.Source);
        Assert.Null(manual.ImportComment);
        Assert.Null(imported.UpdatedAtUtc);
    }

    private class Fixture
    {
        public Guid UserId { get; } = Guid.NewGuid();
        public DateTime Date { get; } = new(2026, 10, 13, 12, 30, 0);
        public Account Account { get; }
        public Account Target { get; }
        public Mock<ITestStorage> Work { get; } = new(MockBehavior.Strict);
        public List<MoneyMovement> Saved { get; } = [];
        public List<MoneyMovement> Created { get; } = [];
        public List<Receipt> Receipts { get; } = [];
        public List<MoneyMovementReceipt> Links { get; } = [];
        public int SaveCount { get; private set; }
        public int AccountCreates { get; private set; }
        public int AccountDeletes { get; private set; }

        public Fixture(AccountAccessRole role = AccountAccessRole.Owner)
        {
            Guid owner = UserId;
            if (role != AccountAccessRole.Owner)
            {
                owner = Guid.NewGuid();
            }
            Account = TestAccountFactory.Create(ownerUserId: owner);
            Target = TestAccountFactory.Create(ownerUserId: UserId);
            if (role != AccountAccessRole.Owner)
            {
                Assert.True(Account.TryAddMember(UserId, role, out _, out _));
            }

            // Имитируем FK fixup после назначения сохранённого ID счёта тестовой фабрикой.
            foreach (Account account in new[] { Account, Target })
            {
                foreach (AccountMember member in account.Members)
                {
                    typeof(AccountMember).GetProperty(nameof(AccountMember.AccountId))!.SetValue(member, account.Id);
                }
            }

            Mock<IAccountMemberRepository> members = new(MockBehavior.Strict);
            members.Setup(x => x.GetItemByPredicateAsync(It.IsAny<Expression<Func<AccountMember, bool>>>(),
                true, It.IsAny<Func<IQueryable<AccountMember>, IQueryable<AccountMember>>>(), It.IsAny<CancellationToken>()))
                .Returns((Expression<Func<AccountMember, bool>> predicate, bool tracking,
                    Func<IQueryable<AccountMember>, IQueryable<AccountMember>>? include, CancellationToken ct) =>
                    Task.FromResult(Account.Members.Concat(Target.Members).SingleOrDefault(predicate.Compile())));
            Mock<IMoneyMovementRepository> movements = new(MockBehavior.Strict);
            movements.Setup(x => x.GetItemByPredicateAsync(It.IsAny<Expression<Func<MoneyMovement, bool>>>(),
                It.IsAny<bool>(), It.IsAny<Func<IQueryable<MoneyMovement>, IQueryable<MoneyMovement>>>(), It.IsAny<CancellationToken>()))
                .Returns((Expression<Func<MoneyMovement, bool>> predicate, bool tracking,
                    Func<IQueryable<MoneyMovement>, IQueryable<MoneyMovement>>? include, CancellationToken ct) =>
                    Task.FromResult(Saved.FirstOrDefault(predicate.Compile())));
            movements.Setup(x => x.GetItemsByPredicateAsync(It.IsAny<Expression<Func<MoneyMovement, bool>>>(),
                It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<bool>(),
                It.IsAny<Func<IQueryable<MoneyMovement>, IQueryable<MoneyMovement>>>(), It.IsAny<CancellationToken>()))
                .Returns((Expression<Func<MoneyMovement, bool>> predicate, int skip, int? take, bool tracking,
                    Func<IQueryable<MoneyMovement>, IQueryable<MoneyMovement>>? include, CancellationToken ct) =>
                    Task.FromResult(Saved.Where(predicate.Compile()).ToList()));
            movements.Setup(x => x.Create(It.IsAny<MoneyMovement>())).Callback<MoneyMovement>(Created.Add);
            movements.Setup(x => x.GetItemByIdAsync(It.IsAny<Guid>(), true, null, It.IsAny<CancellationToken>()))
                .Returns((Guid id, bool tracking, Func<IQueryable<MoneyMovement>, IQueryable<MoneyMovement>>? include,
                    CancellationToken ct) => Task.FromResult(Saved.FirstOrDefault(movement => movement.Id == id)));
            Mock<IReceiptRepository> receipts = new(MockBehavior.Strict);
            receipts.Setup(x => x.GetItemByPredicateAsync(It.IsAny<Expression<Func<Receipt, bool>>>(),
                It.IsAny<bool>(), It.IsAny<Func<IQueryable<Receipt>, IQueryable<Receipt>>>(), It.IsAny<CancellationToken>()))
                .Returns((Expression<Func<Receipt, bool>> predicate, bool tracking,
                    Func<IQueryable<Receipt>, IQueryable<Receipt>>? include, CancellationToken ct) =>
                    Task.FromResult(Receipts.FirstOrDefault(predicate.Compile())));
            receipts.Setup(x => x.GetItemsByPredicateAsync(It.IsAny<Expression<Func<Receipt, bool>>>(),
                It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<bool>(),
                It.IsAny<Func<IQueryable<Receipt>, IQueryable<Receipt>>>(), It.IsAny<CancellationToken>()))
                .Returns((Expression<Func<Receipt, bool>> predicate, int skip, int? take, bool tracking,
                    Func<IQueryable<Receipt>, IQueryable<Receipt>>? include, CancellationToken ct) =>
                    Task.FromResult(Receipts.Where(predicate.Compile()).ToList()));
            Mock<IAccountRepository> accounts = new(MockBehavior.Strict);
            accounts.Setup(x => x.GetItemByPredicateAsync(It.IsAny<Expression<Func<Account, bool>>>(),
                It.IsAny<bool>(), It.IsAny<Func<IQueryable<Account>, IQueryable<Account>>>(), It.IsAny<CancellationToken>()))
                .Returns((Expression<Func<Account, bool>> predicate, bool tracking,
                    Func<IQueryable<Account>, IQueryable<Account>>? include, CancellationToken ct) =>
                    Task.FromResult(new[] { Account, Target }.FirstOrDefault(predicate.Compile())));
            Mock<IReceiptAccountRepository> receiptAccounts = new(MockBehavior.Strict);
            receiptAccounts.Setup(x => x.Create(It.IsAny<ReceiptAccount>())).Callback(() => AccountCreates++);
            receiptAccounts.Setup(x => x.Delete(It.IsAny<ReceiptAccount>())).Callback(() => AccountDeletes++);
            Mock<IMoneyMovementReceiptRepository> links = new(MockBehavior.Strict);
            links.Setup(x => x.CreateRange(It.IsAny<IEnumerable<MoneyMovementReceipt>>()))
                .Callback<IEnumerable<MoneyMovementReceipt>>(items => Links.AddRange(items));
            links.Setup(x => x.Create(It.IsAny<MoneyMovementReceipt>())).Callback<MoneyMovementReceipt>(Links.Add);
            links.Setup(x => x.GetItemByPredicateAsync(It.IsAny<Expression<Func<MoneyMovementReceipt, bool>>>(),
                true, null, It.IsAny<CancellationToken>()))
                .Returns((Expression<Func<MoneyMovementReceipt, bool>> predicate, bool tracking,
                    Func<IQueryable<MoneyMovementReceipt>, IQueryable<MoneyMovementReceipt>>? include, CancellationToken ct) =>
                    Task.FromResult(Saved.SelectMany(movement => movement.ReceiptLinks).Concat(Links).FirstOrDefault(predicate.Compile())));

            Work.SetupGet(x => x.MoneyMovement).Returns(movements.Object);
            Work.SetupGet(x => x.Receipt).Returns(receipts.Object);
            Work.SetupGet(x => x.Account).Returns(accounts.Object);
            Work.SetupGet(x => x.AccountMember).Returns(members.Object);
            Work.SetupGet(x => x.ReceiptAccount).Returns(receiptAccounts.Object);
            Work.SetupGet(x => x.MoneyMovementReceipt).Returns(links.Object);
            Work.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(() =>
            {
                SaveCount++;
                foreach (MoneyMovement movement in Created.Except(Saved))
                {
                    movement.Id = Guid.NewGuid();
                    Assert.True(movement.TryMoveToAccount(Account, out _));
                    Saved.Add(movement);
                }
                return Task.CompletedTask;
            });
            Work.Setup(x => x.ExecuteInTransaction(It.IsAny<Func<CancellationToken, Task>>(),
                IsolationLevel.Serializable, It.IsAny<CancellationToken>()))
                .Returns<Func<CancellationToken, Task>, IsolationLevel, CancellationToken>((action, level, ct) => action(ct));
            Work.Setup(x => x.ExecuteInTransaction(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
                .Returns<Func<CancellationToken, Task>, CancellationToken>(async (action, ct) =>
                {
                    await action(ct);
                    await Work.Object.SaveChangesAsync(ct);
                });
        }

        public BankStatementImportRowRequest Row(decimal amount = 100, string importComment = "purchase", string? comment = null)
        {
            return new() { OccurredAt = Date, Amount = amount, Type = MoneyMovementType.Expense,
                ImportComment = importComment, Comment = comment };
        }

        public MoneyMovement Imported(decimal amount = 100)
        {
            return TestMoneyMovementFactory.CreateBankStatementImport(accountId: Account.Id, amount: amount, userId: UserId,
                occurredAt: Date, importComment: "purchase", comment: "old");
        }

        public MoneyMovement Manual()
        {
            MoneyMovement movement = TestMoneyMovementFactory.CreateManual(accountId: Account.Id, userId: UserId,
                occurredAt: Date, type: MoneyMovementType.Expense);
            Assert.True(movement.TryMoveToAccount(Account, out _));
            return movement;
        }

        public Receipt Receipt()
        {
            Receipt receipt = TestReceiptFactory.Create(createdByUserId: UserId, dateTime: Date);
            Assert.True(receipt.TryAddAccount(Account, out _, out _));
            return receipt;
        }

        public Task<ServiceResult<BankStatementImportResultDto>> Import(params BankStatementImportRowRequest[] rows)
        {
            return TestUseCaseFactory.CreateImportMoneyMovementsUseCase(Work.Object).ExecuteAsync(new()
            {
                AccountId = Account.Id, Rows = rows.ToList()
            }, UserId, TestContext.Current.CancellationToken);
        }
    }
}
