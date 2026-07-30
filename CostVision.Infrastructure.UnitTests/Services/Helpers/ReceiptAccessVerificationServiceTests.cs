using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.Enums.Authorization;
using CostVision.Domain.Models.Receipts;
using CostVision.Infrastructure.Services.Helpers;
using Xunit;

namespace CostVision.Infrastructure.UnitTests.Services.Helpers;

public class ReceiptAccessVerificationServiceTests
{
    [Fact]
    public void UserHasAccessToReceipt_ReturnsTrueForReceiptOwner()
    {
        User user = new() { Id = Guid.NewGuid() };
        Receipt receipt = CreateReceipt(user.Id);
        ReceiptAccessVerificationService service = new();

        bool result = service.UserHasAccessToReceipt(user, receipt);

        Assert.True(result);
    }

    [Fact]
    public void UserHasAccessToReceipt_ReturnsTrueForLinkedAccountOwner()
    {
        User user = new() { Id = Guid.NewGuid() };
        Account account = CreateAccount(Guid.NewGuid(), user.Id);
        Receipt receipt = CreateReceiptWithAccount(account);
        ReceiptAccessVerificationService service = new();

        bool result = service.UserHasAccessToReceipt(user, receipt);

        Assert.True(result);
    }

    [Fact]
    public void UserHasAccessToReceipt_ReturnsTrueForLinkedAccountMember()
    {
        User user = new() { Id = Guid.NewGuid() };
        Account account = CreateAccount(Guid.NewGuid(), Guid.NewGuid());
        account.TryAddMember(user.Id, AccountAccessRole.Viewer, out _, out _);
        Receipt receipt = CreateReceiptWithAccount(account);
        ReceiptAccessVerificationService service = new();

        bool result = service.UserHasAccessToReceipt(user, receipt);

        Assert.True(result);
    }

    [Fact]
    public void UserHasAccessToReceipt_ReturnsFalseForUnrelatedUser()
    {
        User user = new() { Id = Guid.NewGuid() };
        Account account = CreateAccount(Guid.NewGuid(), Guid.NewGuid());
        account.TryAddMember(Guid.NewGuid(), AccountAccessRole.Viewer, out _, out _);
        Receipt receipt = CreateReceiptWithAccount(account);
        ReceiptAccessVerificationService service = new();

        bool result = service.UserHasAccessToReceipt(user, receipt);

        Assert.False(result);
    }

    [Fact]
    public void UserHasAccessToReceipt_ReturnsFalseWhenReceiptHasNoAccountLinks()
    {
        User user = new() { Id = Guid.NewGuid() };
        Receipt receipt = CreateReceipt(Guid.NewGuid());
        ReceiptAccessVerificationService service = new();

        bool result = service.UserHasAccessToReceipt(user, receipt);

        Assert.False(result);
    }

    private static Receipt CreateReceiptWithAccount(Account account)
    {
        Receipt receipt = CreateReceipt(Guid.NewGuid());
        Assert.True(receipt.TryAddAccount(account, out _, out string? error), error);
        return receipt;
    }

    private static Receipt CreateReceipt(Guid createdByUserId)
    {
        bool isCreated = Receipt.TryCreate(
            "fn",
            "fd",
            "fp",
            new DateTime(2026, 1, 1),
            CostVision.Domain.Models.Enums.Receipts.ReceiptOperationType.Income,
            100,
            createdByUserId,
            new DateTime(2026, 1, 1),
            out Receipt? receipt,
            out string? error);
        Assert.True(isCreated, error);
        receipt!.Id = Guid.NewGuid();
        return receipt;
    }

    private static Account CreateAccount(Guid accountId, Guid ownerUserId)
    {
        bool isCreated = Account.TryCreate(
            "Account",
            null,
            null,
            ownerUserId,
            new DateTime(2026, 1, 1),
            out Account? account,
            out string? error);
        Assert.True(isCreated, error);
        account!.Id = accountId;
        return account;
    }
}
