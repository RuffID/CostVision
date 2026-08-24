using System.Reflection;
using CostVision.Application.Models.Responses.Results;
using CostVision.Domain.Models.Enums.Authorization;
using CostVision.Domain.Models.Receipts;
using Xunit;

namespace CostVision.Application.UnitTests.UseCases.Receipts.Accounts;

public class AccountReceiptAccessValidatorTests
{
    [Fact]
    public void ValidateModificationAccess_ReturnsSuccess_ForOwner()
    {
        Guid userId = Guid.NewGuid();
        Account account = TestAccountFactory.Create(ownerUserId: userId);

        ServiceResult result = Validate(account, userId);

        Assert.True(result.Success);
    }

    [Fact]
    public void ValidateModificationAccess_ReturnsSuccess_ForEditorMember()
    {
        Guid userId = Guid.NewGuid();
        Account account = TestAccountFactory.Create();
        account.TryAddMember(userId, AccountAccessRole.Editor, out _, out _);

        ServiceResult result = Validate(account, userId);

        Assert.True(result.Success);
    }

    [Fact]
    public void ValidateModificationAccess_ReturnsForbidden_ForForeignUser()
    {
        Account account = TestAccountFactory.Create();

        ServiceResult result = Validate(account, Guid.NewGuid());

        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.Forbidden, result.Error?.Type);
    }

    [Fact]
    public void ValidateModificationAccess_ReturnsForbidden_ForViewerMember()
    {
        Guid userId = Guid.NewGuid();
        Account account = TestAccountFactory.Create();
        account.TryAddMember(userId, AccountAccessRole.Viewer, out _, out _);

        ServiceResult result = Validate(account, userId);

        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.Forbidden, result.Error?.Type);
    }

    private static ServiceResult Validate(Account account, Guid userId)
    {
        Type type = typeof(CostVision.Application.UseCases.Receipts.Accounts.CreateAccountUseCase).Assembly
            .GetType("CostVision.Application.UseCases.Receipts.Accounts.Helpers.AccountReceiptAccessValidator")!;
        MethodInfo method = type.GetMethod("ValidateModificationAccess", BindingFlags.Public | BindingFlags.Static)!;
        return (ServiceResult)method.Invoke(null, [account, userId])!;
    }
}
