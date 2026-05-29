using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.Enums.Authorization;
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
        return new Account
        {
            Id = id,
            Name = name,
            ColorHex = Account.DEFAULT_COLOR_HEX,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = createdByUserId
        };
    }

    public static AccountMember CreateAccountOwner(Guid accountId, Guid userId)
    {
        return new AccountMember
        {
            AccountId = accountId,
            UserId = userId,
            Role = AccountAccessRole.Owner
        };
    }

    public static Product CreateProduct(Guid id, string name)
    {
        return new Product
        {
            Id = id,
            Name = name,
            NormalizedName = $"{name}-{Guid.NewGuid():N}".ToLowerInvariant()
        };
    }

    public static Receipt CreateReceipt(Guid id, Guid createdByUserId, Guid accountId, Guid productId)
    {
        return new Receipt
        {
            Id = id,
            FiscalDriveNumber = "1234567890123456",
            FiscalDocumentNumber = "12345",
            FiscalSign = "987654321",
            DateTime = DateTime.UtcNow,
            OperationType = ReceiptOperationType.Income,
            TotalSum = 100,
            CashTotalSum = 0,
            EcashTotalSum = 100,
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = DateTime.UtcNow,
            Items =
            [
                new ReceiptItem
                {
                    Id = Guid.NewGuid(),
                    ProductId = productId,
                    Price = 100,
                    Quantity = 1,
                    Sum = 100,
                    PaymentType = PaymentType.Electronic,
                    ProductType = ProductType.Product,
                    ItemsQuantityMeasure = QuantityMeasureType.Piece
                }
            ],
            Accounts =
            [
                new ReceiptAccount
                {
                    AccountId = accountId
                }
            ]
        };
    }
}
