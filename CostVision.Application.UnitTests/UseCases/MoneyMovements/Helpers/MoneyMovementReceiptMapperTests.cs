using System.Reflection;
using CostVision.Domain.Models.MoneyMovements;
using CostVision.Domain.Models.Receipts;
using Xunit;

namespace CostVision.Application.UnitTests.UseCases.MoneyMovements.Helpers;

public class MoneyMovementReceiptMapperTests
{
    [Fact]
    public void MapReceiptLinkDto_MapsReceiptAndFirstAccountByName()
    {
        Guid receiptId = Guid.NewGuid();
        Receipt receipt = new()
        {
            Id = receiptId,
            DateTime = new DateTime(2026, 2, 1),
            Store = new Store { Name = "Market" },
            TotalSum = 77m,
            MoneyMovementLinks = [new MoneyMovementReceipt()]
        };
        AddAccountLink(receipt, "Zoo");
        AddAccountLink(receipt, "Alpha");

        dynamic dto = MapReceipt(receipt);

        Assert.Equal(receiptId, dto.ReceiptId);
        Assert.Equal(new DateTime(2026, 2, 1), dto.DateTime);
        Assert.Equal("Market", dto.RetailPlace);
        Assert.Equal(77m, dto.TotalSum);
        Assert.Equal("Alpha", dto.AccountName);
        Assert.True(dto.IsLinkedToOtherMoneyMovement);
    }

    [Fact]
    public void MapReceiptLinkDto_UsesFallbackRetailPlaceAndEmptyAccount()
    {
        Receipt receipt = new()
        {
            Id = Guid.NewGuid(),
            User = "Legal name"
        };

        dynamic dto = MapReceipt(receipt);

        Assert.Equal("Legal name", dto.RetailPlace);
        Assert.Equal(string.Empty, dto.AccountName);
        Assert.False(dto.IsLinkedToOtherMoneyMovement);
    }

    [Fact]
    public void MapReceiptLinkDto_MapsLinkWithoutNestedReceipt()
    {
        Guid receiptId = Guid.NewGuid();
        MoneyMovementReceipt link = new() { ReceiptId = receiptId };

        dynamic dto = MapLink(link);

        Assert.Equal(receiptId, dto.ReceiptId);
        Assert.True(dto.IsLinkedToOtherMoneyMovement);
    }

    private static void AddAccountLink(Receipt receipt, string accountName)
    {
        Guid accountId = Guid.NewGuid();
        Assert.True(receipt.TryAddAccount(accountId, out ReceiptAccount? link, out string? error), error);
        link!.Account = new Account { Id = accountId, Name = accountName };
    }

    private static object MapReceipt(Receipt receipt)
    {
        MethodInfo method = MapperType.GetMethod("MapReceiptLinkDto", BindingFlags.Public | BindingFlags.Static, [typeof(Receipt)])!;
        return method.Invoke(null, [receipt])!;
    }

    private static object MapLink(MoneyMovementReceipt link)
    {
        MethodInfo method = MapperType.GetMethod("MapReceiptLinkDto", BindingFlags.Public | BindingFlags.Static, [typeof(MoneyMovementReceipt)])!;
        return method.Invoke(null, [link])!;
    }

    private static Type MapperType => typeof(CostVision.Application.UseCases.MoneyMovements.BankStatementImports.Parsing.IBankStatementParser)
        .Assembly
        .GetType("CostVision.Application.UseCases.MoneyMovements.Helpers.MoneyMovementReceiptMapper", true)!;
}
