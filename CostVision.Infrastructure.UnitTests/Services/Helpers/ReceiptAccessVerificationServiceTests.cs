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
        Receipt receipt = new() { CreatedByUserId = user.Id };
        ReceiptAccessVerificationService service = new();

        bool result = service.UserHasAccessToReceipt(user, receipt);

        Assert.True(result);
    }

    [Fact]
    public void UserHasAccessToReceipt_ReturnsTrueForLinkedAccountOwner()
    {
        User user = new() { Id = Guid.NewGuid() };
        Receipt receipt = new()
        {
            CreatedByUserId = Guid.NewGuid(),
            Accounts =
            [
                new ReceiptAccount
                {
                    Account = new Account
                    {
                        CreatedByUserId = user.Id
                    }
                }
            ]
        };
        ReceiptAccessVerificationService service = new();

        bool result = service.UserHasAccessToReceipt(user, receipt);

        Assert.True(result);
    }

    [Fact]
    public void UserHasAccessToReceipt_ReturnsTrueForLinkedAccountMember()
    {
        User user = new() { Id = Guid.NewGuid() };
        Account account = new() { CreatedByUserId = Guid.NewGuid() };
        account.TryAddMember(user.Id, AccountAccessRole.Viewer, out _, out _);
        Receipt receipt = new()
        {
            CreatedByUserId = Guid.NewGuid(),
            Accounts =
            [
                new ReceiptAccount
                {
                    Account = account
                }
            ]
        };
        ReceiptAccessVerificationService service = new();

        bool result = service.UserHasAccessToReceipt(user, receipt);

        Assert.True(result);
    }

    [Fact]
    public void UserHasAccessToReceipt_ReturnsFalseForUnrelatedUser()
    {
        User user = new() { Id = Guid.NewGuid() };
        Account account = new() { CreatedByUserId = Guid.NewGuid() };
        account.TryAddMember(Guid.NewGuid(), AccountAccessRole.Viewer, out _, out _);
        Receipt receipt = new()
        {
            CreatedByUserId = Guid.NewGuid(),
            Accounts =
            [
                new ReceiptAccount
                {
                    Account = account
                }
            ]
        };
        ReceiptAccessVerificationService service = new();

        bool result = service.UserHasAccessToReceipt(user, receipt);

        Assert.False(result);
    }

    [Fact]
    public void UserHasAccessToReceipt_ReturnsFalseWhenReceiptHasNoAccountLinks()
    {
        User user = new() { Id = Guid.NewGuid() };
        Receipt receipt = new() { CreatedByUserId = Guid.NewGuid() };
        ReceiptAccessVerificationService service = new();

        bool result = service.UserHasAccessToReceipt(user, receipt);

        Assert.False(result);
    }
}
