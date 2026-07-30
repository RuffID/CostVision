using CostVision.Domain.Models.Receipts;
using Xunit;

namespace CostVision.Application.UnitTests.UseCases.Receipts.ExpenseCategories;

public class ExpenseCategoryTests
{
    [Fact]
    public void TryCreate_NormalizesDetailsAndAddsChildToParent()
    {
        Guid userId = Guid.NewGuid();
        ExpenseCategory parent = CreateCategory("Parent", userId);
        parent.Id = Guid.NewGuid();

        bool success = ExpenseCategory.TryCreate(
            " Child ",
            " Description ",
            userId,
            parent,
            out ExpenseCategory? child,
            out string? error);

        Assert.True(success, error);
        Assert.Equal("Child", child!.Name);
        Assert.Equal("Description", child.Description);
        Assert.Equal(parent.Id, child.ParentId);
        Assert.Same(parent, child.Parent);
        Assert.Same(child, Assert.Single(parent.Children));
    }

    [Fact]
    public void TryUpdateDetails_DoesNotChangeStateWhenDescriptionIsTooLong()
    {
        ExpenseCategory category = CreateCategory("Original", Guid.NewGuid());

        bool success = category.TryUpdateDetails(
            "Changed",
            new string('x', ExpenseCategory.MAX_DESCRIPTION_LENGTH + 1),
            out string? error);

        Assert.False(success);
        Assert.NotNull(error);
        Assert.Equal("Original", category.Name);
        Assert.Null(category.Description);
    }

    [Fact]
    public void TryMoveTo_RejectsCycleWithoutChangingTree()
    {
        Guid userId = Guid.NewGuid();
        ExpenseCategory root = CreateCategory("Root", userId);
        ExpenseCategory child = CreateCategory("Child", userId, root);
        ExpenseCategory grandchild = CreateCategory("Grandchild", userId, child);

        bool success = root.TryMoveTo(grandchild, out string? error);

        Assert.False(success);
        Assert.NotNull(error);
        Assert.Null(root.Parent);
        Assert.Same(child, Assert.Single(root.Children));
        Assert.Same(grandchild, Assert.Single(child.Children));
    }

    [Fact]
    public void ArchiveAndRestore_ChangeArchivedState()
    {
        ExpenseCategory category = CreateCategory("Category", Guid.NewGuid());

        category.Archive();
        Assert.True(category.IsArchived);

        category.Restore();
        Assert.False(category.IsArchived);
    }

    [Fact]
    public void PublicApi_DoesNotExposeCategoryStateForMutation()
    {
        ExpenseCategory category = CreateCategory("Category", Guid.NewGuid());

        Assert.Null(typeof(ExpenseCategory).GetConstructor(Type.EmptyTypes));
        Assert.True(typeof(ExpenseCategory).GetProperty(nameof(ExpenseCategory.Name))!.SetMethod!.IsPrivate);
        Assert.True(typeof(ExpenseCategory).GetProperty(nameof(ExpenseCategory.Description))!.SetMethod!.IsPrivate);
        Assert.True(typeof(ExpenseCategory).GetProperty(nameof(ExpenseCategory.UserId))!.SetMethod!.IsPrivate);
        Assert.True(typeof(ExpenseCategory).GetProperty(nameof(ExpenseCategory.ParentId))!.SetMethod!.IsPrivate);
        Assert.True(typeof(ExpenseCategory).GetProperty(nameof(ExpenseCategory.IsArchived))!.SetMethod!.IsPrivate);
        Assert.Throws<NotSupportedException>(() => ((ICollection<ExpenseCategory>)category.Children).Clear());
        Assert.Throws<NotSupportedException>(() => ((ICollection<ReceiptItem>)category.ReceiptItems).Clear());
    }

    private static ExpenseCategory CreateCategory(
        string name,
        Guid userId,
        ExpenseCategory? parent = null)
    {
        bool isCreated = ExpenseCategory.TryCreate(
            name,
            null,
            userId,
            parent,
            out ExpenseCategory? category,
            out string? error);
        Assert.True(isCreated, error);
        return category!;
    }
}
