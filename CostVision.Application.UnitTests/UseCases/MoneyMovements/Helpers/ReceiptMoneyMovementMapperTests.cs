using System.Reflection;
using CostVision.Domain.Models.Enums.MoneyMovements;
using CostVision.Domain.Models.MoneyMovements;
using CostVision.Domain.Models.Receipts;
using Xunit;

namespace CostVision.Application.UnitTests.UseCases.MoneyMovements.Helpers;

public class ReceiptMoneyMovementMapperTests
{
    [Fact]
    public void MapReceiptMoneyMovementDto_MapsMovementAndDetectsExistingLinks()
    {
        Guid movementId = Guid.NewGuid();
        MoneyMovement movement = new()
        {
            Id = movementId,
            Account = new Account { Name = "Main" },
            Amount = 15m,
            Type = MoneyMovementType.Expense,
            OccurredAt = new DateTime(2026, 2, 1),
            Comment = "comment",
            ImportComment = "import",
            ReceiptLinks = [new MoneyMovementReceipt()]
        };

        dynamic dto = MapMovement(movement);

        Assert.Equal(movementId, dto.MoneyMovementId);
        Assert.Equal(new DateTime(2026, 2, 1), dto.OccurredAt);
        Assert.Equal(15m, dto.Amount);
        Assert.Equal(MoneyMovementType.Expense, dto.Type);
        Assert.Equal("comment", dto.Comment);
        Assert.Equal("import", dto.ImportComment);
        Assert.Equal("Main", dto.AccountName);
        Assert.True(dto.IsLinkedToOtherReceipt);
    }

    [Fact]
    public void MapReceiptMoneyMovementDto_UsesEmptyStringsForNullableFields()
    {
        MoneyMovement movement = new()
        {
            Id = Guid.NewGuid(),
            Amount = 15m,
            Type = MoneyMovementType.Income,
            OccurredAt = new DateTime(2026, 2, 1)
        };

        dynamic dto = MapMovement(movement);

        Assert.Equal(string.Empty, dto.Comment);
        Assert.Equal(string.Empty, dto.ImportComment);
        Assert.Equal(string.Empty, dto.AccountName);
        Assert.False(dto.IsLinkedToOtherReceipt);
    }

    [Fact]
    public void MapReceiptMoneyMovementDto_MapsLinkWithoutNestedMovement()
    {
        Guid movementId = Guid.NewGuid();
        MoneyMovementReceipt link = new() { MoneyMovementId = movementId };

        dynamic dto = MapLink(link);

        Assert.Equal(movementId, dto.MoneyMovementId);
        Assert.True(dto.IsLinkedToOtherReceipt);
    }

    private static object MapMovement(MoneyMovement movement)
    {
        MethodInfo method = MapperType.GetMethod("MapReceiptMoneyMovementDto", BindingFlags.Public | BindingFlags.Static, [typeof(MoneyMovement)])!;
        return method.Invoke(null, [movement])!;
    }

    private static object MapLink(MoneyMovementReceipt link)
    {
        MethodInfo method = MapperType.GetMethod("MapReceiptMoneyMovementDto", BindingFlags.Public | BindingFlags.Static, [typeof(MoneyMovementReceipt)])!;
        return method.Invoke(null, [link])!;
    }

    private static Type MapperType => typeof(CostVision.Application.UseCases.MoneyMovements.BankStatementImports.Parsing.IBankStatementParser)
        .Assembly
        .GetType("CostVision.Application.UseCases.MoneyMovements.Helpers.ReceiptMoneyMovementMapper", true)!;
}
