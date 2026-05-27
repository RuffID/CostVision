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
        Account account = new() { CreatedByUserId = userId };

        ServiceResult<bool> result = Validate(account, userId);

        Assert.True(result.Success);
    }

    [Fact]
    public void ValidateModificationAccess_ReturnsSuccess_ForEditorMember()
    {
        Guid userId = Guid.NewGuid();
        Account account = new()
        {
            CreatedByUserId = Guid.NewGuid(),
            Members = [new AccountMember { UserId = userId, Role = AccountAccessRole.Editor }]
        };

        ServiceResult<bool> result = Validate(account, userId);

        Assert.True(result.Success);
    }

    [Fact]
    public void ValidateModificationAccess_ReturnsForbidden_ForForeignUser()
    {
        Account account = new() { CreatedByUserId = Guid.NewGuid() };

        ServiceResult<bool> result = Validate(account, Guid.NewGuid());

        Assert.False(result.Success);
        Assert.Equal(403, result.Error?.StatusCode);
    }

    [Fact]
    public void ValidateModificationAccess_ReturnsForbidden_ForViewerMember()
    {
        Guid userId = Guid.NewGuid();
        Account account = new()
        {
            CreatedByUserId = Guid.NewGuid(),
            Members = [new AccountMember { UserId = userId, Role = AccountAccessRole.Viewer }]
        };

        ServiceResult<bool> result = Validate(account, userId);

        Assert.False(result.Success);
        Assert.Equal(403, result.Error?.StatusCode);
    }

    private static ServiceResult<bool> Validate(Account account, Guid userId)
    {
        Type type = typeof(CostVision.Application.UseCases.Receipts.Accounts.CreateAccountUseCase).Assembly
            .GetType("CostVision.Application.UseCases.Receipts.Accounts.Helpers.AccountReceiptAccessValidator")!;
        MethodInfo method = type.GetMethod("ValidateModificationAccess", BindingFlags.Public | BindingFlags.Static)!;
        return (ServiceResult<bool>)method.Invoke(null, [account, userId])!;
    }
}
