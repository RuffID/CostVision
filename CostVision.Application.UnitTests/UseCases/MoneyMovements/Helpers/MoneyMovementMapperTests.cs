using System.Reflection;
using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.Enums.MoneyMovements;
using CostVision.Domain.Models.MoneyMovements;
using CostVision.Domain.Models.Receipts;
using Xunit;

namespace CostVision.Application.UnitTests.UseCases.MoneyMovements.Helpers;

public class MoneyMovementMapperTests
{
    [Fact]
    public void MapDto_MapsDomainMovementAndLinkedReceipts()
    {
        Guid movementId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        MoneyMovement movement = new()
        {
            Id = movementId,
            AccountId = accountId,
            Account = new Account { Id = accountId, Name = "Main", ColorHex = "#112233" },
            Amount = 123.45m,
            Type = MoneyMovementType.Expense,
            OccurredAt = new DateTime(2026, 2, 1, 10, 0, 0),
            Comment = "comment",
            ImportComment = "import",
            PerformedByUserId = userId,
            PerformedByUser = new User { Id = userId, Name = "User" },
            Source = MoneyMovementSource.BankStatementImport,
            ReceiptLinks =
            [
                new MoneyMovementReceipt { Receipt = new Receipt { TotalSum = 100m } },
                new MoneyMovementReceipt { Receipt = new Receipt { TotalSum = 23.45m } },
                new MoneyMovementReceipt()
            ]
        };

        dynamic dto = MapDto(movement, 7);

        Assert.Equal(movementId, dto.Id);
        Assert.Equal(accountId, dto.AccountId);
        Assert.Equal("Main", dto.AccountName);
        Assert.Equal("#112233", dto.AccountColorHex);
        Assert.Equal(123.45m, dto.Amount);
        Assert.Equal(MoneyMovementType.Expense, dto.Type);
        Assert.Equal("comment", dto.Comment);
        Assert.Equal("import", dto.ImportComment);
        Assert.Equal(userId, dto.PerformedByUserId);
        Assert.Equal("User", dto.PerformedByUserName);
        Assert.Equal(MoneyMovementSource.BankStatementImport, dto.Source);
        Assert.Equal(3, dto.LinkedReceiptCount);
        Assert.Equal(7, dto.AvailableReceiptCount);
        Assert.Equal(123.45m, dto.LinkedReceiptsTotalSum);
    }

    [Fact]
    public void MapDto_UsesEmptyStringsForMissingNavigationData()
    {
        MoneyMovement movement = new()
        {
            Id = Guid.NewGuid(),
            AccountId = Guid.NewGuid(),
            Amount = 10m,
            Type = MoneyMovementType.Income,
            OccurredAt = new DateTime(2026, 2, 2),
            PerformedByUserId = Guid.NewGuid(),
            Source = MoneyMovementSource.Manual
        };

        dynamic dto = MapDto(movement);

        Assert.Equal(string.Empty, dto.AccountName);
        Assert.Equal(string.Empty, dto.AccountColorHex);
        Assert.Equal(string.Empty, dto.PerformedByUserName);
        Assert.Equal(0, dto.LinkedReceiptCount);
        Assert.Equal(0m, dto.LinkedReceiptsTotalSum);
    }

    private static object MapDto(MoneyMovement movement, int availableReceiptCount = 0)
    {
        Type mapperType = typeof(MoneyMovement).Assembly.GetType("CostVision.Application.UseCases.MoneyMovements.Helpers.MoneyMovementMapper")
            ?? typeof(CostVision.Application.UseCases.MoneyMovements.BankStatementImports.Parsing.IBankStatementParser).Assembly.GetType(
                "CostVision.Application.UseCases.MoneyMovements.Helpers.MoneyMovementMapper",
                true)!;
        MethodInfo method = mapperType.GetMethod("MapDto", BindingFlags.Public | BindingFlags.Static)!;

        return method.Invoke(null, [movement, availableReceiptCount])!;
    }
}
