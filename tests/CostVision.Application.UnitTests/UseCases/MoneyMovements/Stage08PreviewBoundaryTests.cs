using System.Linq.Expressions;
using CostVision.Application.Abstractions.DataBase.Repositories.MoneyMovements;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.Abstractions.Service.MoneyMovements;
using CostVision.Application.Models.Dtos.MoneyMovements;
using CostVision.Application.Models.Requests.MoneyMovements;
using CostVision.Application.UseCases.MoneyMovements;
using CostVision.Application.UseCases.MoneyMovements.BankStatementImports.Parsing;
using CostVision.Domain.Models.Enums.Authorization;
using CostVision.Domain.Models.Receipts;
using Moq;
using Xunit;

namespace CostVision.Application.UnitTests.UseCases.MoneyMovements;

public class Stage08PreviewBoundaryTests
{
    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, 3)]
    [InlineData(true, 0)]
    public async Task MissingOrEmptyFile_IsRejectedBeforeParsingOrStorage(bool hasStream, long length)
    {
        // Arrange
        Mock<IMoneyMovementRepository> movements = new(MockBehavior.Strict);
        Mock<IAccountMemberRepository> members = new(MockBehavior.Strict);
        Mock<IBankStatementParserRegistry> registry = new(MockBehavior.Strict);
        Mock<IBankStatementPdfTextExtractor> extractor = new(MockBehavior.Strict);
        using MemoryStream stream = new([1, 2, 3]);
        PreviewBankStatementImportRequest request = new() { FileLength = length };
        if (hasStream)
        {
            request.FileStream = stream;
        }

        PreviewBankStatementImportUseCase useCase = new(movements.Object, members.Object, registry.Object, extractor.Object);

        // Act
        ServiceResult<BankStatementImportPreviewDto> result = await useCase.ExecuteAsync(request, Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.Validation, result.Error!.Type);
        Assert.Equal("Выберите файл выписки.", result.Error.Message);
        registry.VerifyNoOtherCalls();
        members.VerifyNoOtherCalls();
        movements.VerifyNoOtherCalls();
        extractor.VerifyNoOtherCalls();
        Assert.True(stream.CanRead);
    }

    [Theory]
    [InlineData(AccountAccessRole.Owner, true)]
    [InlineData(AccountAccessRole.Editor, true)]
    [InlineData(AccountAccessRole.Viewer, true)]
    [InlineData(AccountAccessRole.Editor, false)]
    public async Task PreviewRequest_PreservesAccessPredicate_ResultAndCallerOwnedStream(AccountAccessRole role, bool hasAccess)
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        Account account;
        AccountMember membership;
        if (role == AccountAccessRole.Owner)
        {
            account = TestAccountFactory.Create(ownerUserId: userId);
            membership = AccountMember.CreateOwner(account, userId);
        }
        else
        {
            account = TestAccountFactory.Create(ownerUserId: Guid.NewGuid());
            membership = AccountMember.Create(account, userId, role);
        }

        List<AccountMember> saved = [];
        if (hasAccess)
        {
            saved.Add(membership);
        }

        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryStream stream = new([1, 2, 3]);
        PreviewBankStatementImportRequest request = new()
        {
            BankId = "test", AccountId = account.Id, FileName = "statement.pdf", FileLength = 3, FileStream = stream
        };
        Mock<IMoneyMovementRepository> movements = new(MockBehavior.Strict);
        Mock<IAccountMemberRepository> members = new(MockBehavior.Strict);
        Expression<Func<AccountMember, bool>>? actualPredicate = null;
        members.Setup(item => item.GetItemByPredicateAsync(It.IsAny<Expression<Func<AccountMember, bool>>>(), true, null, ct))
            .ReturnsAsync((Expression<Func<AccountMember, bool>> predicate, bool noTracking,
                Func<IQueryable<AccountMember>, IQueryable<AccountMember>>? include, CancellationToken token) =>
            {
                actualPredicate = predicate;

                return saved.AsQueryable().FirstOrDefault(predicate);
            });
        Mock<IBankStatementParser> parser = new(MockBehavior.Strict);
        parser.SetupGet(item => item.IsConfigured).Returns(true);
        parser.Setup(item => item.CanParseFile("statement.pdf")).Returns(true);
        Mock<IBankStatementParserRegistry> registry = new(MockBehavior.Strict);
        registry.Setup(item => item.FindByBankId("test")).Returns(parser.Object);
        Mock<IBankStatementPdfTextExtractor> extractor = new(MockBehavior.Strict);
        IReadOnlyList<string> pages = new[] { "text" };
        BankStatementImportPreviewDto expected = new()
        {
            AccountId = account.Id,
            Errors = [new() { LineNumber = 2, Message = "Unrecognized row" }]
        };
        bool allowed = hasAccess && role != AccountAccessRole.Viewer;
        if (allowed)
        {
            extractor.Setup(item => item.ExtractPagesAsync(stream, ct)).ReturnsAsync(pages);
            parser.Setup(item => item.Parse(account.Id, pages)).Returns(expected);
        }

        PreviewBankStatementImportUseCase useCase = new(movements.Object, members.Object, registry.Object, extractor.Object);

        // Act
        ServiceResult<BankStatementImportPreviewDto> result = await useCase.ExecuteAsync(request, userId, ct);

        // Assert
        Assert.NotNull(actualPredicate);
        Assert.True(new[] { membership }.AsQueryable().Any(actualPredicate));
        Assert.False(new[] { AccountMember.Create(account, Guid.NewGuid(), role) }.AsQueryable().Any(actualPredicate));
        Account otherAccount = TestAccountFactory.Create();
        Assert.False(new[] { AccountMember.Create(otherAccount, userId, role) }.AsQueryable().Any(actualPredicate));
        Assert.Equal(allowed, result.Success);
        if (allowed)
        {
            Assert.Same(expected, result.Data);
            Assert.Equal(2, Assert.Single(result.Data!.Errors).LineNumber);
            extractor.VerifyAll();
            parser.Verify(item => item.Parse(account.Id, pages), Times.Once);
        }
        else
        {
            if (hasAccess)
            {
                Assert.Equal(ServiceErrorType.Forbidden, result.Error!.Type);
            }
            else
            {
                Assert.Equal(ServiceErrorType.NotFound, result.Error!.Type);
            }

            extractor.VerifyNoOtherCalls();
            parser.Verify(item => item.Parse(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<string>>()), Times.Never);
        }

        movements.VerifyNoOtherCalls();
        members.VerifyAll();
        Assert.True(stream.CanRead);
        Assert.Equal(0, stream.Position);
    }
}
