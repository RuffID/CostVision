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
        MoneyMovement movement = TestMoneyMovementFactory.CreateBankStatementImport(
            movementId,
            accountId,
            123.45m,
            new DateTime(2026, 2, 1, 10, 0, 0),
            userId,
            MoneyMovementType.Expense,
            "comment",
            "import",
            "Main",
            "#112233",
            "User");
        TestReceiptFactory.AddMoneyMovementLink(TestReceiptFactory.Create(totalSum: 100m), movement);
        TestReceiptFactory.AddMoneyMovementLink(TestReceiptFactory.Create(totalSum: 23.45m), movement);
        TestMoneyMovementFactory.AddMaterializedReceiptLink(
            movement,
            TestMoneyMovementFactory.CreateLink(movement.Id, Guid.NewGuid(), userId));

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
        MoneyMovement movement = TestMoneyMovementFactory.CreateManualWithoutNavigations(
            amount: 10m,
            occurredAt: new DateTime(2026, 2, 2),
            type: MoneyMovementType.Income);

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
