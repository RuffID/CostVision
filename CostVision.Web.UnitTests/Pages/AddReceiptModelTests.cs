using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.Models.Services.Receipts;
using CostVision.Application.UseCases.Receipts.Accounts;
using CostVision.Application.UseCases.Receipts.Receipts;
using CostVision.Domain.Models.Authorization;
using CostVision.Web.Pages;
using CostVision.Web.UnitTests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace CostVision.Web.UnitTests.Pages;

public class AddReceiptModelTests
{
    [Fact]
    public async Task OnPostManualAsync_CallsSaveManualReceiptUseCase()
    {
        User currentUser = TestUsers.Create();
        ReceiptManualCreateRequest request = new();

        Dependencies dependencies = new();
        dependencies.SaveManualReceiptUseCase
            .Setup(useCase => useCase.ExecuteAsync(request, currentUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ManualReceiptResult { IsCreated = true });

        AddReceiptModel model = dependencies.CreateModel(currentUser);

        IActionResult result = await model.OnPostManualAsync(request, CancellationToken.None);

        JsonResult json = Assert.IsType<JsonResult>(result);
        AddReceiptManualResponse response = JsonResultAssert.Data<AddReceiptManualResponse>(json);
        Assert.True(response.IsCreated);
        dependencies.SaveManualReceiptUseCase.VerifyAll();
    }

    [Fact]
    public async Task OnPostAsync_CallsSaveReceiptsScannedUseCase()
    {
        User currentUser = TestUsers.Create();
        QrScanRequest request = new()
        {
            Results = [new QrScanResult { FileName = "receipt.jpg", DecodedText = "qr" }]
        };

        Dependencies dependencies = new();
        dependencies.SaveReceiptsScannedUseCase
            .Setup(useCase => useCase.ExecuteAsync(request, currentUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReceiptScanResultSummary { ScannedCount = 1, AddedToDbCount = 1 });

        AddReceiptModel model = dependencies.CreateModel(currentUser);

        IActionResult result = await model.OnPostAsync(request, CancellationToken.None);

        JsonResult json = Assert.IsType<JsonResult>(result);
        AddReceiptScanResponse response = JsonResultAssert.Data<AddReceiptScanResponse>(json);
        Assert.Equal(1, response.ScannedCount);
        Assert.Equal(1, response.AddedToDbCount);
        dependencies.SaveReceiptsScannedUseCase.VerifyAll();
    }

    [Fact]
    public async Task OnPostManualAsync_ReturnsMappedError_WhenAccountAccessFails()
    {
        User currentUser = TestUsers.Create();
        ReceiptManualCreateRequest request = new() { AccountId = Guid.NewGuid() };

        Dependencies dependencies = new();
        dependencies.ValidateReceiptCreationAccessUseCase
            .Setup(useCase => useCase.ExecuteAsync(request.AccountId, currentUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<bool>.Fail(403, "Нет доступа."));

        AddReceiptModel model = dependencies.CreateModel(currentUser);

        IActionResult result = await model.OnPostManualAsync(request, CancellationToken.None);

        JsonResult json = Assert.IsType<JsonResult>(result);
        JsonResultAssert.Failure(json, 403, "Нет доступа.");
        dependencies.SaveManualReceiptUseCase.Verify(
            useCase => useCase.ExecuteAsync(It.IsAny<ReceiptManualCreateRequest>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private sealed class Dependencies
    {
        public Mock<IGetUserAccountsForReceiptCreationUseCase> GetUserAccountsForReceiptCreationUseCase { get; } = new(MockBehavior.Strict);
        public Mock<IValidateReceiptCreationAccessUseCase> ValidateReceiptCreationAccessUseCase { get; } = new(MockBehavior.Strict);
        public Mock<ISaveReceiptsScannedUseCase> SaveReceiptsScannedUseCase { get; } = new(MockBehavior.Strict);
        public Mock<ISaveManualReceiptUseCase> SaveManualReceiptUseCase { get; } = new(MockBehavior.Strict);

        public AddReceiptModel CreateModel(User currentUser)
        {
            return new AddReceiptModel(
                GetUserAccountsForReceiptCreationUseCase.Object,
                ValidateReceiptCreationAccessUseCase.Object,
                SaveReceiptsScannedUseCase.Object,
                SaveManualReceiptUseCase.Object)
            {
                CurrentUser = currentUser
            };
        }
    }
}
