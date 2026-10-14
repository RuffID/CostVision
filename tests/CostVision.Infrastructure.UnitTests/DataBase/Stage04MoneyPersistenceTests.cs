using CostVision.Tests;
using System.Data;
using System.Globalization;
using System.Linq.Expressions;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.MoneyMovements;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.UseCases.MoneyMovements;
using CostVision.Domain.Models.Enums.Authorization;
using CostVision.Domain.Models.Enums.MoneyMovements;
using CostVision.Domain.Models.MoneyMovements;
using CostVision.Domain.Models.Receipts;
using CostVision.Infrastructure.DataBase;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace CostVision.Infrastructure.UnitTests.DataBase;

public class Stage04MoneyPersistenceTests
{
    [Fact]
    public void Model_ProtectsOnlyLinkPairAndHasNoImportIdentityConstraint()
    {
        // Arrange
        using ApplicationContext context = CreateContext();
        var movement = context.Model.FindEntityType(typeof(MoneyMovement))!;
        var link = context.Model.FindEntityType(typeof(MoneyMovementReceipt))!;

        // Act
        var indexes = movement.GetIndexes().ToList();
        var key = link.FindPrimaryKey()!;

        // Assert
        Assert.DoesNotContain(indexes, index => index.IsUnique);
        Assert.Equal(new[] { "MoneyMovementId", "ReceiptId" }, key.Properties.Select(property => property.Name));
        Assert.DoesNotContain(link.GetIndexes(), index => index.IsUnique);
        Assert.Equal(18, movement.FindProperty("Amount")!.GetPrecision());
        Assert.Equal(2, movement.FindProperty("Amount")!.GetScale());
        Assert.Equal(1024, movement.FindProperty("ImportComment")!.GetMaxLength());
    }

    [Theory]
    [InlineData("ru-RU")]
    [InlineData("en-US")]
    public async Task ActualImportPredicate_TranslatesFullKeyWithoutExecutingSql(string culture)
    {
        // Arrange
        using ApplicationContext context = CreateContext();
        CultureInfo previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            Guid userId = Guid.NewGuid();
            Assert.True(Account.TryCreate("Account", null, null, Guid.NewGuid(), DateTime.UtcNow, out Account? account, out _));
            account!.Id = Guid.NewGuid();
            Assert.True(account.TryAddMember(userId, AccountAccessRole.Editor, out AccountMember? member, out _));
            Mock<IAccountMemberRepository> members = new(MockBehavior.Strict);
            members.Setup(x => x.GetItemByPredicateAsync(It.IsAny<Expression<Func<AccountMember, bool>>>(), true,
                It.IsAny<Func<IQueryable<AccountMember>, IQueryable<AccountMember>>>(), It.IsAny<CancellationToken>())).ReturnsAsync(member);
            string? sql = null;
            Mock<IMoneyMovementRepository> movements = new(MockBehavior.Strict);
            movements.Setup(x => x.GetItemsByPredicateAsync(It.IsAny<Expression<Func<MoneyMovement, bool>>>(),
                It.IsAny<int>(), It.IsAny<int?>(), false, null, It.IsAny<CancellationToken>()))
                .Returns((Expression<Func<MoneyMovement, bool>> predicate, int skip, int? take, bool tracking,
                    Func<IQueryable<MoneyMovement>, IQueryable<MoneyMovement>>? include, CancellationToken ct) =>
                {
                    sql = context.Set<MoneyMovement>().Where(predicate).ToQueryString();
                    return Task.FromResult(new List<MoneyMovement>());
                });
            movements.Setup(x => x.Create(It.IsAny<MoneyMovement>()));
            Mock<ITestStorage> work = new(MockBehavior.Strict);
            work.SetupGet(x => x.AccountMember).Returns(members.Object);
            work.SetupGet(x => x.MoneyMovement).Returns(movements.Object);
            work.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            work.Setup(x => x.ExecuteInTransaction(It.IsAny<Func<CancellationToken, Task>>(), IsolationLevel.Serializable,
                It.IsAny<CancellationToken>())).Returns<Func<CancellationToken, Task>, IsolationLevel, CancellationToken>((action, isolation, ct) => action(ct));

            // Act
            var result = await TestUseCaseFactory.CreateImportMoneyMovementsUseCase(work.Object).ExecuteAsync(new()
            {
                AccountId = account.Id,
                Rows = [new() { Amount = 123.45m, OccurredAt = new DateTime(2026, 10, 13),
                    Type = MoneyMovementType.Expense, ImportComment = " purchase " }]
            }, userId, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(sql);
            foreach (string field in new[] { "AccountId", "Source", "OccurredAt", "Amount", "Type", "ImportComment" })
            {
                Assert.Contains("[" + field + "]", sql);
            }
            Assert.Contains("123.45", sql);
            Assert.Contains("purchase", sql);
            Assert.DoesNotContain("Invoke", sql);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    private static ApplicationContext CreateContext()
    {
        return new(new DbContextOptionsBuilder<ApplicationContext>()
            .UseSqlServer("Server=unused;Database=unused;User Id=unused;Password=unused;TrustServerCertificate=True").Options);
    }
}
