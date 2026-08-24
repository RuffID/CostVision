using CostVision.Domain.Models.Receipts;
using CostVision.Domain.Models.Enums.Authorization;
using Xunit;

namespace CostVision.Application.UnitTests.UseCases.Receipts.Accounts;

public class AccountTests
{
    [Theory]
    [InlineData("#abcdef", "#ABCDEF")]
    [InlineData(" #123ABC ", "#123ABC")]
    public void TryCreate_NormalizesColor_WhenValueIsValid(string value, string expected)
    {
        bool success = Account.TryCreate("Основной", null, value, Guid.NewGuid(), DateTime.UtcNow, out Account? account, out string? error);

        Assert.True(success, error);
        Assert.Equal(expected, account?.ColorHex);
    }

    [Fact]
    public void TryCreate_UsesDefaultColor_WhenValueIsEmpty()
    {
        bool success = Account.TryCreate("Основной", null, " ", Guid.NewGuid(), DateTime.UtcNow, out Account? account, out string? error);

        Assert.True(success, error);
        Assert.Equal(Account.DEFAULT_COLOR_HEX, account?.ColorHex);
    }

    [Theory]
    [InlineData("ABCDEF")]
    [InlineData("#12")]
    [InlineData("red")]
    public void TryCreate_ReturnsError_WhenColorIsInvalid(string value)
    {
        bool success = Account.TryCreate("Основной", null, value, Guid.NewGuid(), DateTime.UtcNow, out Account? account, out string? error);

        Assert.False(success);
        Assert.Null(account);
        Assert.Equal("Некорректный цвет счёта.", error);
    }

    [Fact]
    public void TryUpdateDetails_DoesNotChangeState_WhenDataIsInvalid()
    {
        Account.TryCreate("Основной", "Описание", "#123456", Guid.NewGuid(), DateTime.UtcNow, out Account? account, out string? creationError);
        Assert.NotNull(account);
        Assert.Null(creationError);

        bool success = account.TryUpdateDetails(" ", "Новое описание", "#ABCDEF", out string? error);

        Assert.False(success);
        Assert.NotNull(error);
        Assert.Equal("Основной", account.Name);
        Assert.Equal("Описание", account.Description);
        Assert.Equal("#123456", account.ColorHex);
    }

    [Fact]
    public void TryCreate_AddsSingleOwnerMember()
    {
        Guid ownerUserId = Guid.NewGuid();

        bool success = Account.TryCreate("Основной", null, null, ownerUserId, DateTime.UtcNow, out Account? account, out string? error);

        Assert.True(success, error);
        AccountMember owner = Assert.Single(account!.Members);
        Assert.Equal(ownerUserId, owner.UserId);
        Assert.Equal(AccountAccessRole.Owner, owner.Role);
    }

    [Fact]
    public void TryAddMember_RejectsDuplicateAndOwnerRole()
    {
        Account.TryCreate("Основной", null, null, Guid.NewGuid(), DateTime.UtcNow, out Account? account, out _);
        Guid memberUserId = Guid.NewGuid();

        Assert.True(account!.TryAddMember(memberUserId, AccountAccessRole.Viewer, out _, out _));
        Assert.False(account.TryAddMember(memberUserId, AccountAccessRole.Editor, out _, out _));
        Assert.False(account.TryAddMember(Guid.NewGuid(), AccountAccessRole.Owner, out _, out _));
        Assert.Equal(2, account.Members.Count);
    }

    [Fact]
    public void TryChangeAndRemoveMember_ProtectOwner()
    {
        Guid ownerUserId = Guid.NewGuid();
        Account.TryCreate("Основной", null, null, ownerUserId, DateTime.UtcNow, out Account? account, out _);
        Guid memberUserId = Guid.NewGuid();
        account!.TryAddMember(memberUserId, AccountAccessRole.Viewer, out _, out _);

        Assert.True(account.TryChangeMemberRole(memberUserId, AccountAccessRole.Editor, out _));
        Assert.Equal(AccountAccessRole.Editor, account.Members.Single(member => member.UserId == memberUserId).Role);
        Assert.False(account.TryChangeMemberRole(ownerUserId, AccountAccessRole.Viewer, out _));
        Assert.False(account.TryRemoveMember(ownerUserId, out _, out _));
        Assert.True(account.TryRemoveMember(memberUserId, out _, out _));
        Assert.Single(account.Members);
    }

    [Fact]
    public void PublicApi_DoesNotExposeInvariantStateForMutation()
    {
        Account.TryCreate("Основной", null, null, Guid.NewGuid(), DateTime.UtcNow, out Account? account, out _);
        Assert.NotNull(account);

        Assert.Null(typeof(Account).GetConstructor(Type.EmptyTypes));
        Assert.True(typeof(Account).GetProperty(nameof(Account.Name))!.SetMethod!.IsPrivate);
        Assert.True(typeof(Account).GetProperty(nameof(Account.Description))!.SetMethod!.IsPrivate);
        Assert.True(typeof(Account).GetProperty(nameof(Account.ColorHex))!.SetMethod!.IsPrivate);
        Assert.True(typeof(Account).GetProperty(nameof(Account.IsArchived))!.SetMethod!.IsPrivate);
        Assert.False(account!.Members is List<AccountMember>);
        Assert.False(account.ReceiptLinks is List<ReceiptAccount>);
    }
}
