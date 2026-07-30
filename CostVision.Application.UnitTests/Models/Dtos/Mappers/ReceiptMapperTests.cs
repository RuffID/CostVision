using CostVision.Application.Models.Dtos.Mappers;
using CostVision.Domain.Models.Enums.Authorization;
using CostVision.Domain.Models.Enums.MoneyMovements;
using CostVision.Domain.Models.MoneyMovements;
using CostVision.Domain.Models.Receipts;
using Xunit;

namespace CostVision.Application.UnitTests.Models.Dtos.Mappers;

public class ReceiptMapperTests
{
    [Fact]
    public void MapReceiptDto_MapsReceiptWithAccountsAndMoneyMovements()
    {
        Guid userId = Guid.NewGuid();
        Guid receiptId = Guid.NewGuid();
        Account editableAccount = CreateAccount("Editable", "#111111", userId, userId, AccountAccessRole.Editor);
        Account readonlyAccount = CreateAccount("Readonly", "#222222", Guid.NewGuid(), userId, AccountAccessRole.Viewer);
        Receipt receipt = TestReceiptFactory.Create(
            receiptId,
            userId,
            new DateTime(2026, 2, 1),
            50m,
            store: TestReceiptFactory.CreateStore("Store"));
        MoneyMovement firstMovement = TestMoneyMovementFactory.CreateManual(
            amount: 10m,
            type: MoneyMovementType.Expense);
        MoneyMovement secondMovement = TestMoneyMovementFactory.CreateManual(
            amount: 20m,
            type: MoneyMovementType.Expense);
        TestReceiptFactory.AddMoneyMovementLink(receipt, firstMovement);
        TestReceiptFactory.AddMoneyMovementLink(receipt, secondMovement);
        TestReceiptFactory.AddMoneyMovementLink(receipt);
        AddAccount(receipt, readonlyAccount);
        AddAccount(receipt, editableAccount);
        AddItem(receipt, TestReceiptFactory.CreateProduct("Milk", "Milk 2.5%"), 2m, 60m, 120m);
        AddItem(receipt, TestReceiptFactory.CreateProduct("Bread"), 1m, 45m, 45m);

        var dto = receipt.MapReceiptDto(userId);

        Assert.Equal(receiptId, dto.Id);
        Assert.Equal("Store", dto.RetailPlace);
        Assert.Equal(string.Empty, dto.RetailPlaceAddress);
        Assert.Equal("fd", dto.FiscalDocumentNumber);
        Assert.Equal("fn", dto.FiscalDriveNumber);
        Assert.Equal("fp", dto.FiscalSign);
        Assert.Equal(50m, dto.TotalSum);
        Assert.Equal(3, dto.MoneyMovementCount);
        Assert.Equal(30m, dto.MoneyMovementsTotalSum);
        Assert.Collection(dto.Items,
            item =>
            {
                Assert.Equal("Milk 2.5%", item.Name);
                Assert.Equal(2m, item.Quantity);
                Assert.Equal(60m, item.Price);
                Assert.Equal(120m, item.Sum);
            },
            item =>
            {
                Assert.Equal("Bread", item.Name);
                Assert.Equal(1m, item.Quantity);
                Assert.Equal(45m, item.Price);
                Assert.Equal(45m, item.Sum);
            });
        Assert.Equal(editableAccount.Id, dto.AccountId);
        Assert.Equal("Editable", dto.AccountName);
        Assert.Collection(dto.Accounts,
            account =>
            {
                Assert.Equal(editableAccount.Id, account.Id);
                Assert.True(account.CanEditReceipt);
            },
            account =>
            {
                Assert.Equal(readonlyAccount.Id, account.Id);
                Assert.False(account.CanEditReceipt);
            });
    }

    [Fact]
    public void MapReceiptDto_UsesUserNameWhenStoreIsMissing()
    {
        Receipt receipt = TestReceiptFactory.Create(Guid.NewGuid(), user: "Legal seller");

        var dto = receipt.MapReceiptDto();

        Assert.Equal("Legal seller", dto.RetailPlace);
        Assert.Null(dto.AccountId);
        Assert.Equal(string.Empty, dto.AccountName);
        Assert.Empty(dto.Accounts);
    }

    private static Account CreateAccount(string name, string colorHex, Guid ownerId, Guid memberId, AccountAccessRole role)
    {
        Guid accountId = Guid.NewGuid();
        Account account = TestAccountFactory.Create(accountId, ownerId, name, colorHex: colorHex);

        if (memberId != ownerId)
            account.TryAddMember(memberId, role, out _, out _);

        return account;
    }

    private static void AddAccount(Receipt receipt, Account account)
    {
        Assert.True(receipt.TryAddAccount(account, out _, out string? error), error);
    }

    private static void AddItem(Receipt receipt, Product product, decimal quantity, decimal price, decimal sum)
    {
        Assert.True(ReceiptItem.TryCreate(price, quantity, sum, 0, default, default, default, product, null, out ReceiptItem? item, out string? error), error);
        Assert.True(receipt.TryAddItem(item!, out error), error);
    }
}
