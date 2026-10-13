using CostVision.Application.Extensions;
using CostVision.Domain.Models.Enums.Receipts;
using CostVision.Domain.Models.Receipts;
using CostVision.Infrastructure.DataBase;
using CostVision.Infrastructure.DataBase.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Globalization;
using Xunit;

namespace CostVision.Infrastructure.UnitTests.DataBase;

public class Stage03ReceiptPersistenceTests
{
    [Fact]
    public void Model_ProtectsAuthorCopyAndExactLink_ButHasNoGlobalDocumentConstraint()
    {
        // Arrange
        using ApplicationContext context = CreateContext();
        IEntityType receipt = context.Model.FindEntityType(typeof(Receipt))!;
        IEntityType link = context.Model.FindEntityType(typeof(ReceiptAccount))!;

        // Act
        IIndex authorIdentity = receipt.GetIndexes().Single(index => index.GetDatabaseName() == ReceiptWriteErrors.AUTHOR_IDENTITY_INDEX);
        IKey linkKey = link.FindPrimaryKey()!;

        // Assert
        Assert.True(authorIdentity.IsUnique);
        Assert.Equal(new[] { "CreatedByUserId", "FiscalDriveNumber", "FiscalDocumentNumber", "FiscalSign", "DateTime", "TotalSum", "OperationType" },
            authorIdentity.Properties.Select(property => property.Name));
        Assert.Equal(new[] { "ReceiptId", "AccountId" }, linkKey.Properties.Select(property => property.Name));
        Assert.Equal(ReceiptWriteErrors.ACCOUNT_LINK_KEY, linkKey.GetName());
        Assert.DoesNotContain(receipt.GetIndexes(), index => index.IsUnique && !index.Properties.Any(property => property.Name == "CreatedByUserId"));
        Assert.DoesNotContain(link.GetProperties(), property => property.Name == "FiscalSign");
        Assert.Equal(18, receipt.FindProperty("TotalSum")!.GetPrecision());
        Assert.Equal(2, receipt.FindProperty("TotalSum")!.GetScale());
    }

    [Theory]
    [InlineData("ru-RU")]
    [InlineData("en-US")]
    public void IdentityPredicatesAndAccountsInclude_TranslateWithoutExecutingSql(string culture)
    {
        // Arrange
        CultureInfo previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            using ApplicationContext context = CreateContext();
            Guid author = Guid.NewGuid();
            Guid account = Guid.NewGuid();
            Assert.True(Receipt.TryCreate("fn", "fd", "fp", new DateTime(2026, 1, 1),
                ReceiptOperationType.Income, 123.45m, author, new DateTime(2026, 1, 1), out Receipt? receipt, out _));
            ReceiptIdentity identity = receipt!.GetIdentity();

            // Act
            string authorQuery = context.Set<Receipt>().Where(identity.ForAuthorCopy(author))
                .Include(item => item.Accounts).ToQueryString();
            string accountQuery = context.Set<Receipt>().Where(identity.InAccount(account, Guid.NewGuid())).ToQueryString();

            // Assert
            foreach (string field in new[] { "FiscalDriveNumber", "FiscalDocumentNumber", "FiscalSign", "DateTime", "TotalSum", "OperationType" })
            {
                Assert.Contains("[" + field + "]", authorQuery);
                Assert.Contains("[" + field + "]", accountQuery);
            }

            Assert.Contains("LEFT JOIN [ReceiptAccounts]", authorQuery);
            Assert.Contains("CreatedByUserId", authorQuery);
            Assert.Contains("123.45", authorQuery);
            Assert.Contains("EXISTS", accountQuery);
            Assert.Contains("[AccountId]", accountQuery);
            Assert.Contains("<>", accountQuery);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData(2601, "unique index", true)]
    [InlineData(2627, "UNIQUE KEY constraint", true)]
    [InlineData(2601, "уникальным индексом", true)]
    [InlineData(547, "unique index", false)]
    [InlineData(1205, "unique index", false)]
    [InlineData(0, "unique index", false)]
    public void UniqueClassifier_RequiresKnownConstraintAndSqlNumber(int number, string prefix, bool expected)
    {
        // Arrange
        string message = prefix + " '" + ReceiptWriteErrors.AUTHOR_IDENTITY_INDEX + "'.";

        // Act
        bool known = ReceiptWriteErrors.IsKnownUniqueViolation(number, message);

        // Assert
        Assert.Equal(expected, known);
        Assert.False(ReceiptWriteErrors.IsKnownUniqueViolation(number, prefix + " 'IX_Products_NormalizedName'."));
        Assert.False(ReceiptWriteErrors.IsKnownUniqueViolation(number,
            "Violation of UNIQUE KEY constraint 'OtherConstraint'. The duplicate key value is ('PK_ReceiptAccounts')."));
        Assert.False(ReceiptWriteErrors.IsDeadlock(new DbUpdateException("unrelated")));
    }

    [Fact]
    public void ExactLinkPrimaryKey_IsKnownUniqueConflict()
    {
        // Arrange
        string message = "Violation of PRIMARY KEY constraint 'PK_ReceiptAccounts'.";

        // Act
        bool known = ReceiptWriteErrors.IsKnownUniqueViolation(2627, message);

        // Assert
        Assert.True(known);
    }

    private static ApplicationContext CreateContext()
    {
        DbContextOptions<ApplicationContext> options = new DbContextOptionsBuilder<ApplicationContext>()
            .UseSqlServer("Server=unused;Database=unused;User Id=unused;Password=unused;TrustServerCertificate=True")
            .Options;
        return new ApplicationContext(options);
    }
}
