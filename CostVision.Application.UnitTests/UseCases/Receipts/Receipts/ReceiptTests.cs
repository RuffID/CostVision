using CostVision.Domain.Models.Enums.Receipts;
using CostVision.Domain.Models.Receipts;
using Xunit;

namespace CostVision.Application.UnitTests.UseCases.Receipts.Receipts;

public class ReceiptTests
{
    [Fact]
    public void TryCreate_TrimsFiscalNumbersAndCreatesValidReceipt()
    {
        Guid userId = Guid.NewGuid();
        DateTime createdAtUtc = new(2026, 7, 30, 10, 0, 0, DateTimeKind.Utc);

        bool success = Receipt.TryCreate(
            "  fn  ",
            "  fd  ",
            "  fp  ",
            new DateTime(2026, 7, 29, 12, 0, 0),
            ReceiptOperationType.Expense,
            100,
            userId,
            createdAtUtc,
            out Receipt? receipt,
            out string? error);

        Assert.True(success, error);
        Assert.Equal("fn", receipt!.FiscalDriveNumber);
        Assert.Equal("fd", receipt.FiscalDocumentNumber);
        Assert.Equal("fp", receipt.FiscalSign);
        Assert.Equal(userId, receipt.CreatedByUserId);
        Assert.Equal(createdAtUtc, receipt.CreatedAtUtc);
    }

    [Fact]
    public void TryCreate_RejectsNonPositiveTotalSum()
    {
        bool success = Receipt.TryCreate(
            "fn",
            "fd",
            "fp",
            new DateTime(2026, 7, 29),
            ReceiptOperationType.Expense,
            0,
            Guid.NewGuid(),
            DateTime.UtcNow,
            out Receipt? receipt,
            out string? error);

        Assert.False(success);
        Assert.Null(receipt);
        Assert.NotNull(error);
    }

    [Fact]
    public void TryAddAccount_RejectsDuplicateWithoutChangingState()
    {
        Receipt receipt = CreateReceipt();
        Guid accountId = Guid.NewGuid();
        Assert.True(receipt.TryAddAccount(accountId, out ReceiptAccount? originalLink, out string? error), error);

        bool success = receipt.TryAddAccount(accountId, out ReceiptAccount? duplicateLink, out error);

        Assert.False(success);
        Assert.Null(duplicateLink);
        Assert.Same(originalLink, Assert.Single(receipt.Accounts));
    }

    [Fact]
    public void TryMoveAccount_DoesNotChangeStateWhenTargetAlreadyLinked()
    {
        Receipt receipt = CreateReceipt();
        Guid sourceAccountId = Guid.NewGuid();
        Guid targetAccountId = Guid.NewGuid();
        Assert.True(receipt.TryAddAccount(sourceAccountId, out ReceiptAccount? sourceLink, out string? error), error);
        Assert.True(receipt.TryAddAccount(targetAccountId, out ReceiptAccount? targetLink, out error), error);

        bool success = receipt.TryMoveAccount(
            sourceAccountId,
            targetAccountId,
            out _,
            out _,
            out error);

        Assert.False(success);
        Assert.Equal(2, receipt.Accounts.Count);
        Assert.Contains(sourceLink, receipt.Accounts);
        Assert.Contains(targetLink, receipt.Accounts);
    }

    [Fact]
    public void TryRefreshFrom_DoesNotChangeStateWhenItemIsInvalid()
    {
        Receipt receipt = CreateReceipt();
        Product originalProduct = new() { Name = "Old", NormalizedName = "OLD" };
        Assert.True(ReceiptItem.TryCreate(10, 1, 10, 0, default, default, default, originalProduct, null, out ReceiptItem? originalItem, out string? error), error);
        Assert.True(receipt.TryAddItem(originalItem!, out error), error);
        Receipt source = CreateReceipt("new-fn", "new-fd", "new-fp", 20);
        ReceiptItem invalidItem = new()
        {
            Product = new Product { Name = "Invalid", NormalizedName = "INVALID" },
            Price = 20,
            Quantity = 0,
            Sum = 20
        };

        bool success = receipt.TryRefreshFrom(
            source,
            null,
            [invalidItem],
            DateTime.UtcNow,
            out error);

        Assert.False(success);
        Assert.Equal("fn", receipt.FiscalDriveNumber);
        Assert.Same(originalItem, Assert.Single(receipt.Items));
    }

    [Fact]
    public void ReceiptItemTryCreate_RejectsInvalidQuantity()
    {
        Product product = new() { Name = "Product", NormalizedName = "PRODUCT" };

        bool success = ReceiptItem.TryCreate(
            10,
            0,
            10,
            0,
            default,
            default,
            default,
            product,
            null,
            out ReceiptItem? item,
            out string? error);

        Assert.False(success);
        Assert.Null(item);
        Assert.NotNull(error);
    }

    private static Receipt CreateReceipt(
        string fiscalDriveNumber = "fn",
        string fiscalDocumentNumber = "fd",
        string fiscalSign = "fp",
        decimal totalSum = 10)
    {
        Assert.True(Receipt.TryCreate(
            fiscalDriveNumber,
            fiscalDocumentNumber,
            fiscalSign,
            new DateTime(2026, 7, 29),
            ReceiptOperationType.Expense,
            totalSum,
            Guid.NewGuid(),
            new DateTime(2026, 7, 30),
            out Receipt? receipt,
            out string? error),
            error);

        return receipt!;
    }
}
