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
        Receipt receipt = new()
        {
            Id = receiptId,
            CreatedByUserId = userId,
            DateTime = new DateTime(2026, 2, 1),
            Store = new Store { Name = "Store" },
            FiscalDocumentNumber = null!,
            FiscalDriveNumber = null!,
            FiscalSign = null!,
            TotalSum = 50m,
            Accounts =
            [
                new ReceiptAccount { ReceiptId = receiptId, AccountId = readonlyAccount.Id, Account = readonlyAccount },
                new ReceiptAccount { ReceiptId = receiptId, AccountId = editableAccount.Id, Account = editableAccount }
            ],
            MoneyMovementLinks =
            [
                new MoneyMovementReceipt { MoneyMovement = new MoneyMovement { Amount = 10m, Type = MoneyMovementType.Expense } },
                new MoneyMovementReceipt { MoneyMovement = new MoneyMovement { Amount = 20m, Type = MoneyMovementType.Expense } },
                new MoneyMovementReceipt()
            ],
            Items =
            [
                new ReceiptItem
                {
                    Product = new Product { Name = "Milk", AdaptiveName = "Milk 2.5%" },
                    Quantity = 2m,
                    Price = 60m,
                    Sum = 120m
                },
                new ReceiptItem
                {
                    Product = new Product { Name = "Bread" },
                    Quantity = 1m,
                    Price = 45m,
                    Sum = 45m
                }
            ]
        };

        var dto = receipt.MapReceiptDto(userId);

        Assert.Equal(receiptId, dto.Id);
        Assert.Equal("Store", dto.RetailPlace);
        Assert.Equal(string.Empty, dto.RetailPlaceAddress);
        Assert.Equal(string.Empty, dto.FiscalDocumentNumber);
        Assert.Equal(string.Empty, dto.FiscalDriveNumber);
        Assert.Equal(string.Empty, dto.FiscalSign);
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
        Receipt receipt = new()
        {
            Id = Guid.NewGuid(),
            CreatedByUserId = Guid.NewGuid(),
            User = "Legal seller"
        };

        var dto = receipt.MapReceiptDto();

        Assert.Equal("Legal seller", dto.RetailPlace);
        Assert.Null(dto.AccountId);
        Assert.Equal(string.Empty, dto.AccountName);
        Assert.Empty(dto.Accounts);
    }

    private static Account CreateAccount(string name, string colorHex, Guid ownerId, Guid memberId, AccountAccessRole role)
    {
        Guid accountId = Guid.NewGuid();
        return new Account
        {
            Id = accountId,
            Name = name,
            ColorHex = colorHex,
            CreatedByUserId = ownerId,
            Members = [new AccountMember { AccountId = accountId, UserId = memberId, Role = role }]
        };
    }
}
