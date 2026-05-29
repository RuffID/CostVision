using CostVision.Application.Models.Dtos.Mappers;
using CostVision.Domain.Models.Enums.Authorization;
using CostVision.Domain.Models.MoneyMovements;
using CostVision.Domain.Models.Receipts;
using Xunit;

namespace CostVision.Application.UnitTests.Models.Dtos.Mappers;

public class ReceiptGroupMapperTests
{
    [Fact]
    public void MapReceiptGroupDto_MergesAccountsAndAggregatesMoneyMovementTotals()
    {
        Guid currentUserId = Guid.NewGuid();
        Guid editableReceiptId = Guid.NewGuid();
        Account editableAccount = CreateAccount("Editable", currentUserId, currentUserId, AccountAccessRole.Editor);
        Account readonlyAccount = CreateAccount("Readonly", Guid.NewGuid(), currentUserId, AccountAccessRole.Viewer);
        Receipt editableReceipt = CreateReceipt(editableReceiptId, currentUserId, editableAccount, 10m);
        Receipt readonlyReceipt = CreateReceipt(Guid.NewGuid(), Guid.NewGuid(), readonlyAccount, 20m);

        var dto = new[] { readonlyReceipt, editableReceipt }.MapReceiptGroupDto(currentUserId);

        Assert.Equal(editableReceiptId, dto.Id);
        Assert.Equal(editableAccount.Id, dto.AccountId);
        Assert.Equal("Editable", dto.AccountName);
        Assert.Equal(2, dto.MoneyMovementCount);
        Assert.Equal(30m, dto.MoneyMovementsTotalSum);
        Assert.Equal(["Editable", "Readonly"], dto.Accounts.Select(account => account.Name).ToList());
        Assert.True(dto.Accounts.Single(account => account.Id == editableAccount.Id).CanEditReceipt);
        Assert.False(dto.Accounts.Single(account => account.Id == readonlyAccount.Id).CanEditReceipt);
    }

    [Fact]
    public void MapReceiptGroupDto_UsesPrimaryAccountWhenNoEditableAccountsExist()
    {
        Guid currentUserId = Guid.NewGuid();
        Account account = CreateAccount("Account", Guid.NewGuid(), currentUserId, AccountAccessRole.Viewer);
        Receipt receipt = CreateReceipt(Guid.NewGuid(), Guid.NewGuid(), account, 5m);

        var dto = new[] { receipt }.MapReceiptGroupDto(currentUserId);

        Assert.Equal(receipt.Id, dto.Id);
        Assert.Equal(account.Id, dto.AccountId);
        Assert.Equal("Account", dto.AccountName);
    }

    [Fact]
    public void MapReceiptGroupDto_ReturnsEmptyDtoForEmptyList()
    {
        var dto = Array.Empty<Receipt>().MapReceiptGroupDto(Guid.NewGuid());

        Assert.Equal(Guid.Empty, dto.Id);
        Assert.Null(dto.AccountId);
        Assert.Equal(string.Empty, dto.AccountName);
        Assert.Empty(dto.Accounts);
        Assert.Empty(dto.Items);
    }

    private static Receipt CreateReceipt(Guid receiptId, Guid createdByUserId, Account account, decimal linkedAmount)
    {
        return new Receipt
        {
            Id = receiptId,
            CreatedByUserId = createdByUserId,
            RetailPlace = "Store",
            Accounts = [new ReceiptAccount { ReceiptId = receiptId, AccountId = account.Id, Account = account }],
            MoneyMovementLinks = [new MoneyMovementReceipt { MoneyMovement = new MoneyMovement { Amount = linkedAmount } }]
        };
    }

    private static Account CreateAccount(string name, Guid ownerId, Guid memberId, AccountAccessRole role)
    {
        Guid accountId = Guid.NewGuid();
        return new Account
        {
            Id = accountId,
            Name = name,
            CreatedByUserId = ownerId,
            Members = [new AccountMember { AccountId = accountId, UserId = memberId, Role = role }]
        };
    }
}
