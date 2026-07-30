using System.Text.Json;
using CostVision.Domain.Models.Enums.Receipts;
using CostVision.Domain.Models.Receipts;
using CostVision.Infrastructure.Abstractions.Api;
using CostVision.Infrastructure.Models.ConfigClass;
using CostVision.Infrastructure.Models.Requests.ProverkachekaApi;
using CostVision.Infrastructure.Models.Responses.ProverkachekaApi;
using CostVision.Infrastructure.Services.Receipts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace CostVision.Infrastructure.UnitTests.Services.Receipts;

public class ExternalReceiptProviderTests
{
    [Fact]
    public async Task GetReceiptAsync_ReturnsMappedReceiptForSuccessfulApiResponse()
    {
        Receipt sourceReceipt = CreateSourceReceipt();
        Mock<IReceiptRequest> receiptRequest = new(MockBehavior.Strict);
        receiptRequest
            .Setup(request => request.GetReceiptByReceiptAsync("https://api.test", It.IsAny<ProverkachekaManualRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSuccessfulResponse());
        ExternalReceiptProvider provider = CreateProvider(receiptRequest.Object);

        var result = await provider.GetReceiptAsync(sourceReceipt, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("shop", result.Data.Store?.Name);
        Assert.Equal(123.45m, result.Data.TotalSum);
        ReceiptItem item = Assert.Single(result.Data.Items);
        Assert.Equal(sourceReceipt.Id, item.ReceiptId);
        Assert.Equal("milk", item.Product?.Name);
        Assert.Equal("MILK", item.Product?.NormalizedName);
    }

    [Fact]
    public async Task GetReceiptAsync_ReturnsFailureWhenApiReturnsNull()
    {
        Mock<IReceiptRequest> receiptRequest = new(MockBehavior.Strict);
        receiptRequest
            .Setup(request => request.GetReceiptByReceiptAsync("https://api.test", It.IsAny<ProverkachekaManualRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProverkachekaResponse?)null);
        ExternalReceiptProvider provider = CreateProvider(receiptRequest.Object);

        var result = await provider.GetReceiptAsync(CreateSourceReceipt(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(500, result.Error?.StatusCode);
    }

    [Fact]
    public async Task GetReceiptAsync_ReturnsFailureWhenApiReturnsErrorCode()
    {
        Mock<IReceiptRequest> receiptRequest = new(MockBehavior.Strict);
        receiptRequest
            .Setup(request => request.GetReceiptByReceiptAsync("https://api.test", It.IsAny<ProverkachekaManualRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProverkachekaResponse
            {
                Code = (int)ReceiptResponseCodeEnum.Incorrect,
                Data = new ProverkachekaData { Error = "bad receipt" }
            });
        ExternalReceiptProvider provider = CreateProvider(receiptRequest.Object);

        var result = await provider.GetReceiptAsync(CreateSourceReceipt(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(500, result.Error?.StatusCode);
        Assert.Contains("bad receipt", result.Error?.Message);
    }

    [Fact]
    public async Task GetReceiptAsync_ReturnsFailureWhenCorrectResponseHasNoJson()
    {
        Mock<IReceiptRequest> receiptRequest = new(MockBehavior.Strict);
        receiptRequest
            .Setup(request => request.GetReceiptByReceiptAsync("https://api.test", It.IsAny<ProverkachekaManualRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProverkachekaResponse
            {
                Code = (int)ReceiptResponseCodeEnum.Correct,
                Data = new ProverkachekaData()
            });
        ExternalReceiptProvider provider = CreateProvider(receiptRequest.Object);

        var result = await provider.GetReceiptAsync(CreateSourceReceipt(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(500, result.Error?.StatusCode);
    }

    [Fact]
    public async Task GetReceiptAsync_PropagatesDeserializationExceptionFromRequest()
    {
        Mock<IReceiptRequest> receiptRequest = new(MockBehavior.Strict);
        receiptRequest
            .Setup(request => request.GetReceiptByReceiptAsync("https://api.test", It.IsAny<ProverkachekaManualRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new JsonException("invalid json"));
        ExternalReceiptProvider provider = CreateProvider(receiptRequest.Object);

        await Assert.ThrowsAsync<JsonException>(() => provider.GetReceiptAsync(CreateSourceReceipt(), CancellationToken.None));
    }

    [Fact]
    public async Task GetReceiptAsync_BuildsManualRequestFromReceiptData()
    {
        Receipt sourceReceipt = CreateSourceReceipt();
        ProverkachekaManualRequest? capturedRequest = null;
        Mock<IReceiptRequest> receiptRequest = new(MockBehavior.Strict);
        receiptRequest
            .Setup(request => request.GetReceiptByReceiptAsync("https://api.test", It.IsAny<ProverkachekaManualRequest>(), It.IsAny<CancellationToken>()))
            .Callback<string, ProverkachekaManualRequest, CancellationToken>((_, request, _) => capturedRequest = request)
            .ReturnsAsync(CreateSuccessfulResponse());
        ExternalReceiptProvider provider = CreateProvider(receiptRequest.Object);

        await provider.GetReceiptAsync(sourceReceipt, CancellationToken.None);

        Assert.NotNull(capturedRequest);
        Assert.Equal("token", capturedRequest.ApiToken);
        Assert.Equal("fd", capturedRequest.Fd);
        Assert.Equal("fn", capturedRequest.Fn);
        Assert.Equal("fp", capturedRequest.Fp);
        Assert.Equal("20260529T1015", capturedRequest.Time);
        Assert.Equal("123.45", capturedRequest.Summ);
        Assert.Equal((int)ReceiptOperationType.Expense, capturedRequest.OperationType);
    }

    private static ExternalReceiptProvider CreateProvider(IReceiptRequest receiptRequest)
    {
        Mock<ILogger<ExternalReceiptProvider>> logger = new(MockBehavior.Loose);

        return new ExternalReceiptProvider(
            receiptRequest,
            Options.Create(new ApiEndpointOptions { ProverkachekaApiUrl = "https://api.test" }),
            Options.Create(new ProverkachekaOptions { ProverkachekaApiToken = "token" }),
            logger.Object);
    }

    private static Receipt CreateSourceReceipt()
    {
        return new Receipt
        {
            Id = Guid.NewGuid(),
            FiscalDocumentNumber = "fd",
            FiscalDriveNumber = "fn",
            FiscalSign = "fp",
            DateTime = new DateTime(2026, 5, 29, 10, 15, 0),
            TotalSum = 123.45m,
            OperationType = ReceiptOperationType.Expense,
            CreatedByUserId = Guid.NewGuid(),
            CreatedAtUtc = new DateTime(2026, 5, 29, 10, 16, 0, DateTimeKind.Utc)
        };
    }

    private static ProverkachekaResponse CreateSuccessfulResponse()
    {
        return new ProverkachekaResponse
        {
            Code = (int)ReceiptResponseCodeEnum.Correct,
            Data = new ProverkachekaData
            {
                Json = new ProverkachekaJson
                {
                    FiscalDriveNumber = "fn",
                    FiscalDocumentNumber = 123,
                    FiscalSign = 456,
                    RetailPlace = "shop",
                    RetailPlaceAddress = "address",
                    DateTime = new DateTime(2026, 5, 29, 10, 15, 0),
                    OperationType = ReceiptOperationType.Expense,
                    TotalSum = 12345,
                    Items =
                    [
                        new ProverkachekaItem
                        {
                            Name = "milk",
                            Price = 12345,
                            Sum = 12345,
                            Quantity = 1,
                            PaymentType = PaymentType.Electronic,
                            ProductType = ProductType.Product,
                            ItemsQuantityMeasure = QuantityMeasureType.Piece
                        }
                    ]
                }
            }
        };
    }
}
