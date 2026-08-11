using CostVision.Domain.Models.Enums.Receipts;
using CostVision.Domain.Models.MoneyMovements;
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
    public void TryRefreshFrom_DoesNotChangeStateWhenItemBelongsToAnotherReceipt()
    {
        Receipt receipt = CreateReceipt();
        Product originalProduct = TestReceiptFactory.CreateProduct("Old");
        Assert.True(ReceiptItem.TryCreate(10, 1, 10, 0, PaymentType.FullPayment, ProductType.Product, QuantityMeasureType.Piece, originalProduct, null, out ReceiptItem? originalItem, out string? error), error);
        Assert.True(receipt.TryAddItem(originalItem!, out error), error);
        Receipt source = CreateReceipt("new-fn", "new-fd", "new-fp", 20);
        Product sourceProduct = TestReceiptFactory.CreateProduct("Source");
        Assert.True(ReceiptItem.TryCreate(20, 1, 20, 0, PaymentType.FullPayment, ProductType.Product, QuantityMeasureType.Piece, sourceProduct, null, out ReceiptItem? sourceItem, out error), error);
        Assert.True(source.TryAddItem(sourceItem!, out error), error);

        bool success = receipt.TryRefreshFrom(
            source,
            null,
            [sourceItem!],
            DateTime.UtcNow,
            out error);

        Assert.False(success);
        Assert.Equal("fn", receipt.FiscalDriveNumber);
        Assert.Same(originalItem, Assert.Single(receipt.Items));
    }

    [Fact]
    public void ReceiptItemTryCreate_RejectsInvalidQuantity()
    {
        Product product = TestReceiptFactory.CreateProduct("Product");

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

    [Fact]
    public void PublicApi_DoesNotExposeAggregateStateForMutation()
    {
        Receipt receipt = CreateReceipt();
        Product product = TestReceiptFactory.CreateProduct("Product");
        ReceiptItem item = TestReceiptFactory.CreateItem(product);

        Assert.Null(typeof(Receipt).GetConstructor(Type.EmptyTypes));
        Assert.Null(typeof(ReceiptItem).GetConstructor(Type.EmptyTypes));
        Assert.Null(typeof(ReceiptAccount).GetConstructor(Type.EmptyTypes));
        Assert.Null(typeof(Store).GetConstructor(Type.EmptyTypes));
        Assert.Null(typeof(Product).GetConstructor(Type.EmptyTypes));
        Assert.True(typeof(Receipt).GetProperty(nameof(Receipt.TotalSum))!.SetMethod!.IsPrivate);
        Assert.True(typeof(ReceiptItem).GetProperty(nameof(ReceiptItem.Quantity))!.SetMethod!.IsPrivate);
        Assert.True(typeof(Store).GetProperty(nameof(Store.Name))!.SetMethod!.IsPrivate);
        Assert.True(typeof(Product).GetProperty(nameof(Product.Name))!.SetMethod!.IsPrivate);
        Assert.False(receipt.Items is List<ReceiptItem>);
        Assert.False(receipt.Accounts is List<ReceiptAccount>);
        Assert.False(receipt.MoneyMovementLinks is List<CostVision.Domain.Models.MoneyMovements.MoneyMovementReceipt>);
        Assert.Null(item.Receipt);
    }

    [Fact]
    public void MoneyMovementReceipt_CannotBeCreatedWithIncompleteMetadata()
    {
        Receipt receipt = CreateReceipt();
        receipt.Id = Guid.NewGuid();

        bool success = MoneyMovementReceipt.TryCreate(
            Guid.NewGuid(),
            receipt.Id,
            Guid.Empty,
            new DateTime(2026, 1, 1),
            out MoneyMovementReceipt? link,
            out string? error);

        Assert.False(success);
        Assert.NotNull(error);
        Assert.Null(link);
        Assert.Empty(receipt.MoneyMovementLinks);
        Assert.Null(typeof(MoneyMovementReceipt).GetConstructor(Type.EmptyTypes));
        Assert.True(typeof(MoneyMovementReceipt).GetProperty(nameof(MoneyMovementReceipt.Receipt))!.SetMethod!.IsPrivate);
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
