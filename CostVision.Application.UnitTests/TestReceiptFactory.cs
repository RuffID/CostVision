using CostVision.Domain.Models.Enums.Receipts;
using CostVision.Domain.Models.MoneyMovements;
using CostVision.Domain.Models.Receipts;
using System.Reflection;

namespace CostVision.Application.UnitTests;

internal static class TestReceiptFactory
{
    public static Store CreateStore(string name)
    {
        bool isCreated = Store.TryCreate(name, name.ToUpperInvariant(), null, null, out Store? store, out string? error);
        if (!isCreated || store == null)
            throw new InvalidOperationException(error ?? "Не удалось создать тестовый магазин.");

        return store;
    }

    public static Product CreateProduct(string name, string? adaptiveName = null)
    {
        bool isCreated = Product.TryCreate(
            name,
            name.ToUpperInvariant(),
            out Product? product,
            out string? error);
        if (!isCreated || product == null)
            throw new InvalidOperationException(error ?? "Не удалось создать тестовый товар.");

        if (!product.TryUpdateAdaptiveName(adaptiveName, out error))
            throw new InvalidOperationException(error ?? "Не удалось заполнить тестовый товар.");

        return product;
    }

    public static Receipt Create(
        Guid? receiptId = null,
        Guid? createdByUserId = null,
        DateTime? dateTime = null,
        decimal totalSum = 100m,
        ReceiptOperationType operationType = ReceiptOperationType.Income,
        Store? store = null,
        string? user = null,
        string fiscalDriveNumber = "fn",
        string fiscalDocumentNumber = "fd",
        string fiscalSign = "fp")
    {
        bool isCreated = Receipt.TryCreate(
            fiscalDriveNumber,
            fiscalDocumentNumber,
            fiscalSign,
            dateTime ?? new DateTime(2026, 1, 1),
            operationType,
            totalSum,
            createdByUserId ?? Guid.NewGuid(),
            new DateTime(2026, 1, 1),
            out Receipt? receipt,
            out string? error);
        if (!isCreated || receipt == null)
            throw new InvalidOperationException(error ?? "Не удалось создать тестовый чек.");

        receipt.Id = receiptId ?? Guid.NewGuid();

        if (store != null)
            receipt.AssignStore(store);

        if (user != null)
        {
            bool isUpdated = receipt.TryUpdateDetails(
                user,
                null,
                null,
                null,
                null,
                0,
                totalSum,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                out error);
            if (!isUpdated)
                throw new InvalidOperationException(error ?? "Не удалось заполнить тестовый чек.");
        }

        return receipt;
    }

    public static ReceiptItem CreateItem(
        Product product,
        decimal price = 100m,
        decimal quantity = 1m,
        decimal sum = 100m,
        Guid? categoryId = null)
    {
        bool isCreated = ReceiptItem.TryCreate(
            price,
            quantity,
            sum,
            0,
            PaymentType.Electronic,
            ProductType.Product,
            QuantityMeasureType.Piece,
            product,
            categoryId,
            out ReceiptItem? item,
            out string? error);
        if (!isCreated || item == null)
            throw new InvalidOperationException(error ?? "Не удалось создать тестовую позицию чека.");

        return item;
    }

    public static void AddMaterializedReceiptItem(Product product, ReceiptItem item)
    {
        FieldInfo field = typeof(Product).GetField("_receiptItems", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Backing field позиций товара не найден.");
        List<ReceiptItem> items = (List<ReceiptItem>)field.GetValue(product)!;
        items.Add(item);
    }

    public static MoneyMovementReceipt AddMoneyMovementLink(Receipt receipt, Guid? moneyMovementId = null)
    {
        bool isCreated = MoneyMovementReceipt.TryCreate(
            moneyMovementId ?? Guid.NewGuid(),
            receipt,
            Guid.NewGuid(),
            new DateTime(2026, 1, 1),
            out MoneyMovementReceipt? link,
            out string? error);
        if (!isCreated || link == null)
            throw new InvalidOperationException(error ?? "Не удалось связать тестовый чек с операцией.");

        return link;
    }

    public static MoneyMovementReceipt AddMoneyMovementLink(Receipt receipt, MoneyMovement movement)
    {
        bool isCreated = MoneyMovementReceipt.TryCreate(
            movement,
            receipt,
            Guid.NewGuid(),
            new DateTime(2026, 1, 1),
            out MoneyMovementReceipt? link,
            out string? error);
        if (!isCreated || link == null)
            throw new InvalidOperationException(error ?? "Не удалось связать тестовый чек с операцией.");

        return link;
    }
}
