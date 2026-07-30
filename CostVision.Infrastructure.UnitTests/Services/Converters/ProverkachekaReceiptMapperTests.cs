using CostVision.Domain.Models.Enums.Receipts;
using CostVision.Domain.Models.Receipts;
using CostVision.Infrastructure.Models.Responses.ProverkachekaApi;
using CostVision.Infrastructure.Services.Converters;
using Xunit;

namespace CostVision.Infrastructure.UnitTests.Services.Converters;

public class ProverkachekaReceiptMapperTests
{
    [Fact]
    public void MapToReceipt_MapsApiDataToDomainModel()
    {
        ProverkachekaResponse response = new()
        {
            Data = new ProverkachekaData
            {
                Json = new ProverkachekaJson
                {
                    FiscalDriveNumber = "fn",
                    FiscalDocumentNumber = 123,
                    FiscalSign = 456,
                    RetailPlace = "Shop",
                    RetailPlaceAddress = "Address",
                    User = "Seller",
                    UserInn = "1234567890",
                    Region = "54",
                    DateTime = new DateTime(2026, 5, 29, 10, 15, 0),
                    RequestNumber = 7,
                    ShiftNumber = 8,
                    OperationType = ReceiptOperationType.Expense,
                    AppliedTaxationType = TaxationType.UsnIncome,
                    TotalSum = 12345,
                    CashTotalSum = 1000,
                    EcashTotalSum = 11345,
                    Nds18 = 2000,
                    Nds10 = 100,
                    Nds0 = 0,
                    NdsNo = 555,
                    KktRegId = "kkt",
                    NumberKkt = "number"
                }
            }
        };

        Guid userId = Guid.NewGuid();
        DateTime createdAtUtc = new(2026, 5, 29, 11, 0, 0, DateTimeKind.Utc);
        Receipt result = response.MapToReceipt(userId, createdAtUtc);

        Assert.Equal("fn", result.FiscalDriveNumber);
        Assert.Equal("123", result.FiscalDocumentNumber);
        Assert.Equal("456", result.FiscalSign);
        Assert.Null(result.Store);
        Assert.Equal("Seller", result.User);
        Assert.Equal("1234567890", result.UserInn);
        Assert.Equal("54", result.Region);
        Assert.Equal(new DateTime(2026, 5, 29, 10, 15, 0), result.DateTime);
        Assert.Equal(7, result.CheckNumber);
        Assert.Equal(8, result.ShiftNumber);
        Assert.Equal(ReceiptOperationType.Expense, result.OperationType);
        Assert.Equal(TaxationType.UsnIncome, result.TaxationType);
        Assert.Equal(123.45m, result.TotalSum);
        Assert.Equal(10m, result.CashTotalSum);
        Assert.Equal(113.45m, result.EcashTotalSum);
        Assert.Equal(20m, result.Nds18);
        Assert.Equal(1m, result.Nds10);
        Assert.Equal(0m, result.Nds0);
        Assert.Equal(5.55m, result.NdsNo);
        Assert.Equal("kkt", result.KktRegId);
        Assert.Equal("number", result.NumberKkt);
        Assert.Equal(userId, result.CreatedByUserId);
        Assert.Equal(createdAtUtc, result.CreatedAtUtc);
    }

    [Fact]
    public void MapToReceiptItem_MapsItemAmountsAndEnums()
    {
        Guid productId = Guid.NewGuid();
        Guid categoryId = Guid.NewGuid();
        Product product = new() { Id = productId, Name = "Product", NormalizedName = "PRODUCT" };
        ProverkachekaItem item = new()
        {
            Price = 1999,
            Sum = 3998,
            Quantity = 2,
            Nds = 6,
            PaymentType = PaymentType.Electronic,
            ProductType = ProductType.Service,
            ItemsQuantityMeasure = QuantityMeasureType.Piece
        };

        ReceiptItem result = item.MapToReceiptItem(product, categoryId);

        Assert.Equal(Guid.Empty, result.ReceiptId);
        Assert.Equal(productId, result.ProductId);
        Assert.Equal(categoryId, result.CategoryId);
        Assert.Equal(19.99m, result.Price);
        Assert.Equal(39.98m, result.Sum);
        Assert.Equal(2, result.Quantity);
        Assert.Equal(6, result.Nds);
        Assert.Equal(PaymentType.Electronic, result.PaymentType);
        Assert.Equal(ProductType.Service, result.ProductType);
        Assert.Equal(QuantityMeasureType.Piece, result.ItemsQuantityMeasure);
    }

    [Fact]
    public void MapToReceipt_ThrowsWhenRequiredFieldsAreMissing()
    {
        ProverkachekaResponse response = new()
        {
            Data = new ProverkachekaData
            {
                Json = new ProverkachekaJson
                {
                    FiscalDocumentNumber = 1,
                    FiscalSign = 2
                }
            }
        };

        Assert.Throws<InvalidOperationException>(() =>
            response.MapToReceipt(Guid.NewGuid(), DateTime.UtcNow));
    }
}
