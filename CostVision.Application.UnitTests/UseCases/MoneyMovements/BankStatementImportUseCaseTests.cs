using System.Linq.Expressions;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.MoneyMovements;
using CostVision.Application.Models.Dtos.MoneyMovements;
using CostVision.Application.Models.Requests.MoneyMovements;
using CostVision.Application.UseCases.MoneyMovements;
using CostVision.Application.UseCases.MoneyMovements.BankStatementImports.Parsing;
using CostVision.Domain.Models.Enums.Authorization;
using CostVision.Domain.Models.Enums.MoneyMovements;
using CostVision.Domain.Models.MoneyMovements;
using Moq;
using Xunit;

namespace CostVision.Application.UnitTests.UseCases.MoneyMovements;

public class BankStatementImportUseCaseTests : MoneyMovementUseCaseTestBase
{
    [Fact]
    public async Task PreviewBankStatementImport_ReturnsRowsAndMarksExistingAndPreviewDuplicates()
    {
        Guid userId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();
        DateTime occurredAt = new(2026, 5, 10);
        BankStatementImportPreviewDto preview = new()
        {
            BankId = "test",
            AccountId = accountId,
            Rows =
            [
                CreatePreviewRow(occurredAt, 100, "purchase"),
                CreatePreviewRow(occurredAt, 100, "purchase")
            ]
        };
        MoneyMovement existing = CreateMovement(Guid.NewGuid(), accountId, 100, occurredAt, userId);
        existing.Source = MoneyMovementSource.BankStatementImport;
        existing.Type = MoneyMovementType.Expense;
        existing.ImportComment = "purchase";
        Mock<IBankStatementParser> parser = CreateParser("test", preview);
        Mock<IMoneyMovementRepository> moneyMovementRepository = CreateMoneyMovementRepository();
        moneyMovementRepository
            .Setup(repository => repository.GetItemsByPredicateAsync(
                It.IsAny<Expression<Func<MoneyMovement, bool>>>(),
                It.IsAny<int>(),
                It.IsAny<int?>(),
                true,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([existing]);
        PreviewBankStatementImportUseCase useCase = new(
            CreateUnitOfWork(
                accountMemberRepository: CreateAccountMemberRepositorySequence([CreateMember(accountId, userId, AccountAccessRole.Editor)]),
                moneyMovementRepository: moneyMovementRepository).Object,
            CreateParserRegistry(parser.Object).Object,
            CreatePdfTextExtractor(["page text"]).Object);

        var result = await useCase.ExecuteAsync("test", accountId, "statement.pdf", new MemoryStream([1, 2, 3]), userId, CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(result.Data!.Rows[0].IsDuplicate);
        Assert.Equal(existing.Id, result.Data.Rows[0].DuplicateMoneyMovementId);
        Assert.True(result.Data.Rows[1].IsDuplicate);
    }

    [Fact]
    public async Task PreviewBankStatementImport_KeepsLineErrorsWithoutFailingPreview()
    {
        Guid userId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();
        BankStatementImportPreviewDto preview = new()
        {
            BankId = "test",
            AccountId = accountId,
            Errors = [new BankStatementImportLineErrorDto { LineNumber = 1, Message = "bad", RawText = "raw" }]
        };
        Mock<IBankStatementParser> parser = CreateParser("test", preview);
        PreviewBankStatementImportUseCase useCase = new(
            CreateUnitOfWork(
                accountMemberRepository: CreateAccountMemberRepositorySequence([CreateMember(accountId, userId, AccountAccessRole.Editor)]),
                moneyMovementRepository: CreateMoneyMovementRepository()).Object,
            CreateParserRegistry(parser.Object).Object,
            CreatePdfTextExtractor(["page text"]).Object);

        var result = await useCase.ExecuteAsync("test", accountId, "statement.pdf", new MemoryStream([1]), userId, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(result.Data!.Errors);
    }

    [Fact]
    public async Task PreviewBankStatementImport_ReturnsError_WhenParserCannotParseFile()
    {
        Mock<IBankStatementParser> parser = CreateParser("test", new BankStatementImportPreviewDto());
        parser.Setup(item => item.CanParseFile("statement.txt")).Returns(false);
        PreviewBankStatementImportUseCase useCase = new(
            CreateUnitOfWork().Object,
            CreateParserRegistry(parser.Object).Object,
            CreatePdfTextExtractor(["page text"]).Object);

        var result = await useCase.ExecuteAsync("test", Guid.NewGuid(), "statement.txt", new MemoryStream([1]), Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(400, result.Error?.StatusCode);
    }

    [Fact]
    public async Task PreviewBankStatementImport_ReturnsError_WhenBankIsUnknownOrPdfTextIsEmpty()
    {
        Guid userId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();
        Mock<IBankStatementParser> parser = CreateParser("test", new BankStatementImportPreviewDto());
        PreviewBankStatementImportUseCase unknownBankUseCase = new(
            CreateUnitOfWork().Object,
            CreateParserRegistry(parser.Object).Object,
            CreatePdfTextExtractor(["page text"]).Object);
        PreviewBankStatementImportUseCase emptyPdfUseCase = new(
            CreateUnitOfWork(accountMemberRepository: CreateAccountMemberRepositorySequence([CreateMember(accountId, userId, AccountAccessRole.Editor)])).Object,
            CreateParserRegistry(parser.Object).Object,
            CreatePdfTextExtractor([" "]).Object);

        var unknownBankResult = await unknownBankUseCase.ExecuteAsync("unknown", accountId, "statement.pdf", new MemoryStream([1]), userId, CancellationToken.None);
        var emptyPdfResult = await emptyPdfUseCase.ExecuteAsync("test", accountId, "statement.pdf", new MemoryStream([1]), userId, CancellationToken.None);

        Assert.Equal(400, unknownBankResult.Error?.StatusCode);
        Assert.Equal(400, emptyPdfResult.Error?.StatusCode);
    }

    [Fact]
    public async Task ImportMoneyMovements_CreatesValidRowsInTransaction()
    {
        Guid userId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();
        MoneyMovement? createdMovement = null;
        Mock<IMoneyMovementRepository> moneyMovementRepository = CreateMoneyMovementRepository();
        moneyMovementRepository
            .Setup(repository => repository.GetItemByPredicateAsync(
                It.IsAny<Expression<Func<MoneyMovement, bool>>>(),
                false,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((MoneyMovement?)null);
        moneyMovementRepository.Setup(repository => repository.Create(It.IsAny<MoneyMovement>()))
            .Callback<MoneyMovement>(movement => createdMovement = movement);
        Mock<IUnitOfWork> unitOfWork = CreateUnitOfWork(
            accountMemberRepository: CreateAccountMemberRepositorySequence([CreateMember(accountId, userId, AccountAccessRole.Editor)]),
            moneyMovementRepository: moneyMovementRepository,
            setupTransaction: true);
        ImportMoneyMovementsUseCase useCase = new(unitOfWork.Object);

        var result = await useCase.ExecuteAsync(new SaveBankStatementImportRequest
        {
            AccountId = accountId,
            Rows =
            [
                new BankStatementImportRowRequest
                {
                    OccurredAt = new DateTime(2026, 5, 10),
                    Amount = 100,
                    Type = MoneyMovementType.Expense,
                    Comment = "  card  ",
                    ImportComment = " purchase "
                }
            ]
        }, userId, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(1, result.Data?.CreatedCount);
        Assert.Equal(0, result.Data?.ErrorCount);
        Assert.NotNull(createdMovement);
        Assert.Equal(accountId, createdMovement.AccountId);
        Assert.Equal("card", createdMovement.Comment);
        Assert.Equal("purchase", createdMovement.ImportComment);
        Assert.Equal(MoneyMovementSource.BankStatementImport, createdMovement.Source);
        unitOfWork.Verify(unitOfWork => unitOfWork.ExecuteInTransaction(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ImportMoneyMovements_ReplacesDuplicate_WhenRequested()
    {
        Guid userId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();
        MoneyMovement duplicate = CreateMovement(Guid.NewGuid(), accountId, 50, new DateTime(2026, 5, 9), userId);
        Mock<IMoneyMovementRepository> moneyMovementRepository = CreateMoneyMovementRepository();
        moneyMovementRepository
            .Setup(repository => repository.GetItemByPredicateAsync(
                It.IsAny<Expression<Func<MoneyMovement, bool>>>(),
                false,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(duplicate);
        ImportMoneyMovementsUseCase useCase = new(CreateUnitOfWork(
            accountMemberRepository: CreateAccountMemberRepositorySequence([CreateMember(accountId, userId, AccountAccessRole.Editor)]),
            moneyMovementRepository: moneyMovementRepository,
            setupTransaction: true).Object);

        var result = await useCase.ExecuteAsync(new SaveBankStatementImportRequest
        {
            AccountId = accountId,
            Rows =
            [
                new BankStatementImportRowRequest
                {
                    DuplicateMoneyMovementId = duplicate.Id,
                    ReplaceDuplicate = true,
                    OccurredAt = new DateTime(2026, 5, 10),
                    Amount = 100,
                    Type = MoneyMovementType.Income,
                    ImportComment = "salary"
                }
            ]
        }, userId, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(1, result.Data?.UpdatedCount);
        Assert.Equal(0, result.Data?.ErrorCount);
        Assert.Equal(100, duplicate.Amount);
        Assert.Equal(MoneyMovementType.Income, duplicate.Type);
        Assert.Equal("salary", duplicate.ImportComment);
        Assert.NotNull(duplicate.UpdatedAtUtc);
    }

    [Fact]
    public async Task ImportMoneyMovements_SkipsInvalidRows()
    {
        Guid accountId = Guid.NewGuid();
        ImportMoneyMovementsUseCase useCase = new(CreateUnitOfWork(
            accountMemberRepository: CreateAccountMemberRepositorySequence([CreateMember(accountId, Guid.NewGuid(), AccountAccessRole.Editor)]),
            moneyMovementRepository: CreateMoneyMovementRepository()).Object);

        var result = await useCase.ExecuteAsync(new SaveBankStatementImportRequest
        {
            AccountId = accountId,
            Rows =
            [
                new BankStatementImportRowRequest { Amount = 0, ImportComment = "row" }
            ]
        }, Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(0, result.Data?.CreatedCount);
        Assert.Equal(1, result.Data?.ErrorCount);
        Assert.Single(result.Data!.Errors);
    }

    [Fact]
    public async Task ImportMoneyMovements_SkipsDuplicateWithoutReplace()
    {
        Guid userId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();
        MoneyMovement duplicate = CreateMovement(Guid.NewGuid(), accountId, 100, new DateTime(2026, 5, 10), userId);
        duplicate.Source = MoneyMovementSource.BankStatementImport;
        duplicate.ImportComment = "purchase";
        Mock<IMoneyMovementRepository> moneyMovementRepository = CreateMoneyMovementRepository();
        moneyMovementRepository
            .Setup(repository => repository.GetItemByPredicateAsync(
                It.IsAny<Expression<Func<MoneyMovement, bool>>>(),
                false,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(duplicate);
        ImportMoneyMovementsUseCase useCase = new(CreateUnitOfWork(
            accountMemberRepository: CreateAccountMemberRepositorySequence([CreateMember(accountId, userId, AccountAccessRole.Editor)]),
            moneyMovementRepository: moneyMovementRepository,
            setupTransaction: true).Object);

        var result = await useCase.ExecuteAsync(new SaveBankStatementImportRequest
        {
            AccountId = accountId,
            Rows =
            [
                new BankStatementImportRowRequest
                {
                    OccurredAt = duplicate.OccurredAt,
                    Amount = duplicate.Amount,
                    Type = duplicate.Type,
                    ImportComment = duplicate.ImportComment
                }
            ]
        }, userId, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(0, result.Data?.CreatedCount);
        Assert.Equal(0, result.Data?.UpdatedCount);
        Assert.Equal(1, result.Data?.ErrorCount);
        Assert.Single(result.Data!.Errors);
    }

    [Fact]
    public async Task GetBankStatementImportBanks_ReturnsRegisteredBanks()
    {
        Mock<IBankStatementParser> parser = CreateParser("test", new BankStatementImportPreviewDto());
        parser.SetupGet(item => item.BankName).Returns("Test Bank");
        parser.SetupGet(item => item.Description).Returns("PDF");
        GetBankStatementImportBanksUseCase useCase = new(CreateParserRegistry(parser.Object).Object);

        List<BankStatementImportBankDto> result = await useCase.ExecuteAsync(CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("test", result[0].Id);
        Assert.Equal("Test Bank", result[0].Name);
        Assert.Equal("PDF", result[0].Description);
    }

    [Fact]
    public async Task GetBankStatementImportBanks_ReturnsEmptyList_WhenRegistryIsEmpty()
    {
        Mock<IBankStatementParserRegistry> registry = new(MockBehavior.Strict);
        registry.Setup(item => item.GetParsers()).Returns([]);
        GetBankStatementImportBanksUseCase useCase = new(registry.Object);

        List<BankStatementImportBankDto> result = await useCase.ExecuteAsync(CancellationToken.None);

        Assert.Empty(result);
    }
}
