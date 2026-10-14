using System.Linq.Expressions;
using CostVision.Application.Abstractions.DataBase.Repositories.MoneyMovements;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.Abstractions.Service.MoneyMovements;
using CostVision.Application.Models.Dtos.MoneyMovements;
using CostVision.Application.Models.Requests.MoneyMovements;
using CostVision.Application.UseCases.MoneyMovements;
using CostVision.Application.UseCases.MoneyMovements.BankStatementImports.Parsing;
using CostVision.Domain.Models.Receipts;
using Moq;
using Xunit;

namespace CostVision.Application.UnitTests.UseCases.MoneyMovements;

public class Stage14PreviewTests
{
    [Theory]
    [InlineData("expected")]
    [InlineData("unknown")]
    [InlineData("cancelled")]
    public async Task ActualPreview_MapsOnlyExpectedPdfFailure_WithoutParsingOrStorage(string kind)
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        Account account = TestAccountFactory.Create(ownerUserId: userId);
        AccountMember membership = AccountMember.CreateOwner(account, userId);
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryStream stream = new([1, 2, 3]);
        Mock<IMoneyMovementRepository> movements = new(MockBehavior.Strict);
        Mock<IAccountMemberRepository> members = new(MockBehavior.Strict);
        members.Setup(item => item.GetItemByPredicateAsync(It.IsAny<Expression<Func<AccountMember, bool>>>(), true, null, ct))
            .ReturnsAsync(membership);
        Mock<IBankStatementParser> parser = new(MockBehavior.Strict);
        parser.SetupGet(item => item.IsConfigured).Returns(true);
        parser.Setup(item => item.CanParseFile("statement.pdf")).Returns(true);
        Mock<IBankStatementParserRegistry> registry = new(MockBehavior.Strict);
        registry.Setup(item => item.FindByBankId("test")).Returns(parser.Object);
        Exception failure = new BankStatementPdfException("PDF повреждён.");
        if (kind == "unknown")
        {
            failure = new IOException("test-only unexpected failure");
        }
        else if (kind == "cancelled")
        {
            failure = new OperationCanceledException(ct);
        }

        Mock<IBankStatementPdfTextExtractor> extractor = new(MockBehavior.Strict);
        extractor.Setup(item => item.ExtractPagesAsync(stream, ct)).ThrowsAsync(failure);
        PreviewBankStatementImportUseCase useCase = new(movements.Object, members.Object, registry.Object, extractor.Object);
        PreviewBankStatementImportRequest request = new()
        {
            AccountId = account.Id, BankId = "test", FileName = "statement.pdf", FileLength = 3, FileStream = stream
        };

        // Act
        if (kind == "expected")
        {
            ServiceResult<BankStatementImportPreviewDto> result = await useCase.ExecuteAsync(request, userId, ct);

            // Assert
            Assert.False(result.Success);
            Assert.Equal(ServiceErrorType.Validation, result.Error!.Type);
            Assert.Equal("PDF повреждён.", result.Error.Message);
        }
        else
        {
            Exception? actual = await Record.ExceptionAsync(() => useCase.ExecuteAsync(request, userId, ct));

            // Assert
            Assert.Same(failure, actual);
        }

        // Assert
        parser.Verify(item => item.Parse(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<string>>()), Times.Never);
        movements.VerifyNoOtherCalls();
        extractor.VerifyAll();
        Assert.True(stream.CanRead);
    }
}
