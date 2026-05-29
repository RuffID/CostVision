using System.Linq.Expressions;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.MoneyMovements;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.Abstractions.Service.MoneyMovements;
using CostVision.Application.Models.Dtos.MoneyMovements;
using CostVision.Application.UseCases.MoneyMovements.BankStatementImports.Parsing;
using CostVision.Domain.Models.Enums.Authorization;
using CostVision.Domain.Models.Enums.MoneyMovements;
using CostVision.Domain.Models.Enums.Receipts;
using CostVision.Domain.Models.MoneyMovements;
using CostVision.Domain.Models.Receipts;
using Moq;
using Moq.Language;

namespace CostVision.Application.UnitTests.UseCases.MoneyMovements;

public abstract class MoneyMovementUseCaseTestBase
{
    protected static Mock<IUnitOfWork> CreateUnitOfWork(
        Mock<IAccountRepository>? accountRepository = null,
        Mock<IAccountMemberRepository>? accountMemberRepository = null,
        Mock<IMoneyMovementRepository>? moneyMovementRepository = null,
        Mock<IMoneyMovementReceiptRepository>? moneyMovementReceiptRepository = null,
        Mock<IReceiptRepository>? receiptRepository = null,
        bool setupSaveChanges = false,
        bool setupTransaction = false)
    {
        Mock<IUnitOfWork> unitOfWork = new(MockBehavior.Strict);

        if (accountRepository != null)
            unitOfWork.Setup(item => item.Account).Returns(accountRepository.Object);

        if (accountMemberRepository != null)
            unitOfWork.Setup(item => item.AccountMember).Returns(accountMemberRepository.Object);

        if (moneyMovementRepository != null)
            unitOfWork.Setup(item => item.MoneyMovement).Returns(moneyMovementRepository.Object);

        if (moneyMovementReceiptRepository != null)
            unitOfWork.Setup(item => item.MoneyMovementReceipt).Returns(moneyMovementReceiptRepository.Object);

        if (receiptRepository != null)
            unitOfWork.Setup(item => item.Receipt).Returns(receiptRepository.Object);

        if (setupSaveChanges)
            unitOfWork.Setup(item => item.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        if (setupTransaction)
            unitOfWork.Setup(item => item.ExecuteInTransaction(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
                .Returns<Func<Task>, CancellationToken>((action, _) => action());

        return unitOfWork;
    }

    protected static Mock<IAccountMemberRepository> CreateAccountMemberRepositorySequence(IReadOnlyList<AccountMember?> members)
    {
        Mock<IAccountMemberRepository> repository = new(MockBehavior.Strict);
        ISetupSequentialResult<Task<AccountMember?>> sequence = repository
            .SetupSequence(item => item.GetItemByPredicateAsync(
                It.IsAny<Expression<Func<AccountMember, bool>>>(),
                true,
                null,
                It.IsAny<CancellationToken>()));

        foreach (AccountMember? member in members)
            sequence = sequence.ReturnsAsync(member);

        return repository;
    }

    protected static Mock<IMoneyMovementRepository> CreateMoneyMovementRepository()
    {
        return new Mock<IMoneyMovementRepository>(MockBehavior.Strict);
    }

    protected static Mock<IMoneyMovementReceiptRepository> CreateMoneyMovementReceiptRepository()
    {
        return new Mock<IMoneyMovementReceiptRepository>(MockBehavior.Strict);
    }

    protected static Mock<IReceiptRepository> CreateReceiptRepository()
    {
        return new Mock<IReceiptRepository>(MockBehavior.Strict);
    }

    protected static Mock<IAccountRepository> CreateAccountRepository()
    {
        return new Mock<IAccountRepository>(MockBehavior.Strict);
    }

    protected static AccountMember CreateMember(Guid accountId, Guid userId, AccountAccessRole role)
    {
        return new AccountMember { AccountId = accountId, UserId = userId, Role = role };
    }

    protected static Account CreateAccount(Guid accountId, Guid ownerId, string name = "Account")
    {
        return new Account
        {
            Id = accountId,
            Name = name,
            ColorHex = "#123456",
            CreatedByUserId = ownerId,
            Members = [CreateMember(accountId, ownerId, AccountAccessRole.Owner)]
        };
    }

    protected static MoneyMovement CreateMovement(Guid movementId, Guid accountId, decimal amount, DateTime occurredAt, Guid userId)
    {
        return new MoneyMovement
        {
            Id = movementId,
            AccountId = accountId,
            Amount = amount,
            Type = amount < 0 ? MoneyMovementType.Expense : MoneyMovementType.Income,
            OccurredAt = occurredAt,
            CreatedByUserId = userId,
            PerformedByUserId = userId,
            CreatedAtUtc = new DateTime(2026, 1, 1),
            Source = MoneyMovementSource.Manual
        };
    }

    protected static Receipt CreateReceipt(Guid receiptId, Guid userId, Guid accountId, decimal totalSum, DateTime dateTime)
    {
        Account account = CreateAccount(accountId, userId);
        Receipt receipt = new()
        {
            Id = receiptId,
            CreatedByUserId = userId,
            DateTime = dateTime,
            TotalSum = totalSum,
            OperationType = ReceiptOperationType.Income,
            FiscalDriveNumber = "fn",
            FiscalDocumentNumber = "fd",
            FiscalSign = "fp",
            RetailPlace = "Shop"
        };
        receipt.Accounts.Add(new ReceiptAccount { ReceiptId = receiptId, Receipt = receipt, AccountId = accountId, Account = account });
        return receipt;
    }

    protected static BankStatementImportPreviewRowDto CreatePreviewRow(DateTime occurredAt, decimal amount, string importComment)
    {
        return new BankStatementImportPreviewRowDto
        {
            OccurredAt = occurredAt,
            Amount = amount,
            Type = MoneyMovementType.Expense,
            ImportComment = importComment
        };
    }

    protected static Mock<IBankStatementParser> CreateParser(string bankId, BankStatementImportPreviewDto preview)
    {
        Mock<IBankStatementParser> parser = new(MockBehavior.Strict);
        parser.SetupGet(item => item.BankId).Returns(bankId);
        parser.SetupGet(item => item.BankName).Returns(bankId);
        parser.SetupGet(item => item.Description).Returns("PDF");
        parser.SetupGet(item => item.IsConfigured).Returns(true);
        parser.Setup(item => item.CanParseFile(It.IsAny<string>())).Returns(true);
        parser.Setup(item => item.Parse(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<string>>())).Returns(preview);
        return parser;
    }

    protected static Mock<IBankStatementParserRegistry> CreateParserRegistry(params IBankStatementParser[] parsers)
    {
        Mock<IBankStatementParserRegistry> registry = new(MockBehavior.Strict);
        registry.Setup(item => item.GetParsers()).Returns(parsers);
        registry.Setup(item => item.FindByBankId(It.IsAny<string>()))
            .Returns<string>(bankId => parsers.FirstOrDefault(parser => parser.BankId == bankId));
        return registry;
    }

    protected static Mock<IBankStatementPdfTextExtractor> CreatePdfTextExtractor(IReadOnlyList<string> pages)
    {
        Mock<IBankStatementPdfTextExtractor> extractor = new(MockBehavior.Strict);
        extractor.Setup(item => item.ExtractPagesAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pages);
        return extractor;
    }
}
