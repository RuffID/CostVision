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
            .ReturnsAsync(ServiceResult<AddReceiptManualResponse>.Ok(new AddReceiptManualResponse { Outcome = ManualReceiptOutcome.Created }));

        AddReceiptModel model = dependencies.CreateModel(currentUser);

        IActionResult result = await model.OnPostManualAsync(request, CancellationToken.None);

        JsonResult json = Assert.IsType<JsonResult>(result);
        AddReceiptManualResponse response = JsonResultAssert.Data<AddReceiptManualResponse>(json);
        Assert.Equal(ManualReceiptOutcome.Created, response.Outcome);
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
            .ReturnsAsync(ServiceResult<AddReceiptScanResponse>.Ok(new AddReceiptScanResponse { ScannedCount = 1, AddedToDbCount = 1 }));

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
        dependencies.SaveManualReceiptUseCase
            .Setup(useCase => useCase.ExecuteAsync(request, currentUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<AddReceiptManualResponse>.Fail(ServiceErrorType.Forbidden, "Нет доступа."));

        AddReceiptModel model = dependencies.CreateModel(currentUser);

        IActionResult result = await model.OnPostManualAsync(request, CancellationToken.None);

        JsonResult json = Assert.IsType<JsonResult>(result);
        JsonResultAssert.Failure(json, 403, "Нет доступа.");
        dependencies.SaveManualReceiptUseCase.VerifyAll();
    }

    private sealed class Dependencies
    {
        public Mock<IGetUserAccountsForReceiptCreationUseCase> GetUserAccountsForReceiptCreationUseCase { get; } = new(MockBehavior.Strict);
        public Mock<ISaveReceiptsScannedUseCase> SaveReceiptsScannedUseCase { get; } = new(MockBehavior.Strict);
        public Mock<ISaveManualReceiptUseCase> SaveManualReceiptUseCase { get; } = new(MockBehavior.Strict);

        public AddReceiptModel CreateModel(User currentUser)
        {
            return new AddReceiptModel(
                GetUserAccountsForReceiptCreationUseCase.Object,
                SaveReceiptsScannedUseCase.Object,
                SaveManualReceiptUseCase.Object)
            {
                CurrentUser = currentUser
            };
        }
    }
}
