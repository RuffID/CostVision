using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.Enums.Receipts;
using CostVision.Domain.Models.Receipts;

namespace CostVision.Infrastructure.IntegrationTests.DataBase.Seed;

public static class TestDataFactory
{
    public static User CreateUser(Guid id, string loginPrefix)
    {
        return new User
        {
            Id = id,
            Login = $"{loginPrefix}-{Guid.NewGuid():N}",
            Name = "Integration user",
            PasswordHash = "hash",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public static Account CreateAccount(Guid id, Guid createdByUserId, string name)
    {
        bool isCreated = Account.TryCreate(
            name,
            null,
            null,
            createdByUserId,
            DateTime.UtcNow,
            out Account? account,
            out string? error);
        if (!isCreated || account == null)
            throw new InvalidOperationException(error ?? "Не удалось создать тестовый счёт.");

        account.Id = id;
        return account;
    }

    public static Product CreateProduct(Guid id, string name)
    {
        bool isCreated = Product.TryCreate(
            name,
            $"{name}-{Guid.NewGuid():N}".ToLowerInvariant(),
            out Product? product,
            out string? error);
        if (!isCreated || product == null)
            throw new InvalidOperationException(error ?? "Не удалось создать тестовый товар.");

        product.Id = id;
        return product;
    }

    public static Receipt CreateReceipt(Guid id, Guid createdByUserId, Guid accountId, Product product)
    {
        Receipt.TryCreate(
            "1234567890123456",
            "12345",
            "987654321",
            DateTime.UtcNow,
            ReceiptOperationType.Income,
            100,
            createdByUserId,
            DateTime.UtcNow,
            out Receipt? receipt,
            out _);
        receipt!.Id = id;
        receipt.TryUpdateDetails(null, null, null, null, null, 0, 100, null, null, null, null, null, null, null, out _);
        ReceiptItem.TryCreate(
            100,
            1,
            100,
            0,
            PaymentType.Electronic,
            ProductType.Product,
            QuantityMeasureType.Piece,
            product,
            null,
            out ReceiptItem? item,
            out _);
        item!.Id = Guid.NewGuid();
        receipt.TryAddItem(item, out _);
        receipt.TryAddAccount(accountId, out _, out _);
        return receipt;
    }
}
