using CostVision.Domain.Models.Enums.MoneyMovements;
using CostVision.Domain.Models.MoneyMovements;
using Xunit;

namespace CostVision.Application.UnitTests.UseCases.MoneyMovements;

public class MoneyMovementTests
{
    [Fact]
    public void TryCreateManual_NormalizesAmountAndComment()
    {
        Guid userId = Guid.NewGuid();

        bool success = MoneyMovement.TryCreateManual(
            Guid.NewGuid(),
            -125.50m,
            null,
            new DateTime(2026, 5, 10),
            "  groceries  ",
            userId,
            userId,
            DateTime.UtcNow,
            out MoneyMovement? movement,
            out string? error);

        Assert.True(success, error);
        Assert.Equal(125.50m, movement!.Amount);
        Assert.Equal(MoneyMovementType.Expense, movement.Type);
        Assert.Equal("groceries", movement.Comment);
        Assert.Equal(MoneyMovementSource.Manual, movement.Source);
    }

    [Fact]
    public void TryCreateBankStatementImport_RejectsMissingImportComment()
    {
        bool success = MoneyMovement.TryCreateBankStatementImport(
            Guid.NewGuid(),
            100,
            MoneyMovementType.Expense,
            new DateTime(2026, 5, 10),
            null,
            " ",
            Guid.NewGuid(),
            DateTime.UtcNow,
            out MoneyMovement? movement,
            out string? error);

        Assert.False(success);
        Assert.Null(movement);
        Assert.Equal("У импортируемой операции отсутствует исходный комментарий.", error);
    }

    [Fact]
    public void TryReplaceFromBankStatement_DoesNotChangeState_WhenDataIsInvalid()
    {
        Guid userId = Guid.NewGuid();
        MoneyMovement.TryCreateBankStatementImport(
            Guid.NewGuid(),
            100,
            MoneyMovementType.Expense,
            new DateTime(2026, 5, 10),
            "purchase",
            "source",
            userId,
            DateTime.UtcNow,
            out MoneyMovement? movement,
            out _);

        bool success = movement!.TryReplaceFromBankStatement(
            200,
            MoneyMovementType.Income,
            new DateTime(2026, 5, 11),
            "changed",
            new string('a', MoneyMovement.COMMENT_MAX_LENGTH + 1),
            DateTime.UtcNow,
            out string? error);

        Assert.False(success);
        Assert.NotNull(error);
        Assert.Equal(100, movement.Amount);
        Assert.Equal(MoneyMovementType.Expense, movement.Type);
        Assert.Equal(new DateTime(2026, 5, 10), movement.OccurredAt);
        Assert.Equal("purchase", movement.Comment);
        Assert.Equal("source", movement.ImportComment);
        Assert.Null(movement.UpdatedAtUtc);
    }

    [Fact]
    public void TryUpdateComment_DoesNotChangeState_WhenCommentIsTooLong()
    {
        Guid userId = Guid.NewGuid();
        MoneyMovement.TryCreateManual(
            Guid.NewGuid(),
            100,
            MoneyMovementType.Income,
            new DateTime(2026, 5, 10),
            "original",
            userId,
            userId,
            DateTime.UtcNow,
            out MoneyMovement? movement,
            out _);

        bool success = movement!.TryUpdateComment(
            new string('a', MoneyMovement.COMMENT_MAX_LENGTH + 1),
            out string? error);

        Assert.False(success);
        Assert.NotNull(error);
        Assert.Equal("original", movement.Comment);
    }
}
