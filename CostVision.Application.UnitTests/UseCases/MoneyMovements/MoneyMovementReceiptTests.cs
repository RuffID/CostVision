using CostVision.Domain.Models.MoneyMovements;
using Xunit;

namespace CostVision.Application.UnitTests.UseCases.MoneyMovements;

public class MoneyMovementReceiptTests
{
    [Fact]
    public void TryCreate_CreatesValidLink()
    {
        Guid moneyMovementId = Guid.NewGuid();
        Guid receiptId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        DateTime createdAtUtc = new(2026, 7, 30, 10, 0, 0, DateTimeKind.Utc);

        bool success = MoneyMovementReceipt.TryCreate(
            moneyMovementId,
            receiptId,
            userId,
            createdAtUtc,
            out MoneyMovementReceipt? link,
            out string? error);

        Assert.True(success, error);
        Assert.Equal(moneyMovementId, link!.MoneyMovementId);
        Assert.Equal(receiptId, link.ReceiptId);
        Assert.Equal(userId, link.CreatedByUserId);
        Assert.Equal(createdAtUtc, link.CreatedAtUtc);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void TryCreate_RejectsEmptyIdentifiersWithoutCreatingPartialLink(
        bool emptyMoneyMovementId,
        bool emptyReceiptId,
        bool emptyUserId)
    {
        bool success = MoneyMovementReceipt.TryCreate(
            emptyMoneyMovementId ? Guid.Empty : Guid.NewGuid(),
            emptyReceiptId ? Guid.Empty : Guid.NewGuid(),
            emptyUserId ? Guid.Empty : Guid.NewGuid(),
            DateTime.UtcNow,
            out MoneyMovementReceipt? link,
            out string? error);

        Assert.False(success);
        Assert.Null(link);
        Assert.NotNull(error);
    }

    [Fact]
    public void TryCreate_RejectsMissingCreationDateWithoutCreatingPartialLink()
    {
        bool success = MoneyMovementReceipt.TryCreate(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            default,
            out MoneyMovementReceipt? link,
            out string? error);

        Assert.False(success);
        Assert.Null(link);
        Assert.NotNull(error);
    }
}
