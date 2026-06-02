using CostVision.Application.Models.Dtos.MoneyMovements;
using CostVision.Application.Models.Requests.MoneyMovements;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.MoneyMovements;
using CostVision.Domain.Models.Authorization;
using CostVision.Web.Pages;
using CostVision.Web.UnitTests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace CostVision.Web.UnitTests.Pages;

public class MoneyMovementsModelTests
{
    [Fact]
    public async Task OnGetListAsync_CallsListUseCase()
    {
        User currentUser = TestUsers.Create();
        DateTime dateFrom = new(2026, 5, 1);
        DateTime dateTo = new(2026, 5, 2);
        Guid accountId = Guid.NewGuid();

        Dependencies dependencies = new();
        dependencies.MoneyMovementsPageUseCase
            .Setup(useCase => useCase.GetListAsync(currentUser.Id, dateFrom, dateTo, accountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<List<MoneyMovementDto>>.Ok([new MoneyMovementDto { Id = Guid.NewGuid() }]));

        MoneyMovementsModel model = dependencies.CreateModel(currentUser);

        JsonResult json = await model.OnGetListAsync(dateFrom, dateTo, accountId, CancellationToken.None);

        Assert.Single(JsonResultAssert.Data<List<MoneyMovementDto>>(json));
    }

    [Fact]
    public async Task OnPostCreateDeleteAndMove_CallExpectedUseCases()
    {
        User currentUser = TestUsers.Create();
        CreateMoneyMovementRequest createRequest = new() { AccountId = Guid.NewGuid(), Amount = 100 };
        DeleteMoneyMovementRequest deleteRequest = new() { MoneyMovementId = Guid.NewGuid() };
        MoveMoneyMovementToAccountRequest moveRequest = new() { MoneyMovementId = Guid.NewGuid(), TargetAccountId = Guid.NewGuid() };

        Dependencies dependencies = new();
        dependencies.MoneyMovementsPageUseCase
            .Setup(useCase => useCase.CreateAsync(createRequest, currentUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<MoneyMovementDto>.Ok(new MoneyMovementDto { Id = Guid.NewGuid() }));
        dependencies.MoneyMovementsPageUseCase
            .Setup(useCase => useCase.DeleteAsync(deleteRequest, currentUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<bool>.Ok(true));
        dependencies.MoneyMovementsPageUseCase
            .Setup(useCase => useCase.MoveToAccountAsync(moveRequest, currentUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<bool>.Ok(true));

        MoneyMovementsModel model = dependencies.CreateModel(currentUser);

        JsonResultAssert.Data<MoneyMovementDto>(await model.OnPostCreateAsync(createRequest, CancellationToken.None));
        JsonResultAssert.Data<bool>(await model.OnPostDeleteAsync(deleteRequest, CancellationToken.None));
        JsonResultAssert.Data<bool>(await model.OnPostMoveToAccountAsync(moveRequest, CancellationToken.None));
        dependencies.MoneyMovementsPageUseCase.VerifyAll();
    }

    [Fact]
    public async Task OnPostLinkAndUnlinkReceipt_CallExpectedUseCases()
    {
        User currentUser = TestUsers.Create();
        LinkMoneyMovementReceiptRequest linkRequest = new() { MoneyMovementId = Guid.NewGuid(), ReceiptId = Guid.NewGuid() };
        UnlinkMoneyMovementReceiptRequest unlinkRequest = new() { MoneyMovementId = linkRequest.MoneyMovementId, ReceiptId = linkRequest.ReceiptId };

        Dependencies dependencies = new();
        dependencies.MoneyMovementsPageUseCase
            .Setup(useCase => useCase.LinkReceiptAsync(linkRequest, currentUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<bool>.Ok(true));
        dependencies.MoneyMovementsPageUseCase
            .Setup(useCase => useCase.UnlinkReceiptAsync(unlinkRequest, currentUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<bool>.Ok(true));

        MoneyMovementsModel model = dependencies.CreateModel(currentUser);

        JsonResultAssert.Data<bool>(await model.OnPostLinkReceiptAsync(linkRequest, CancellationToken.None));
        JsonResultAssert.Data<bool>(await model.OnPostUnlinkReceiptAsync(unlinkRequest, CancellationToken.None));
        dependencies.MoneyMovementsPageUseCase.VerifyAll();
    }

    [Fact]
    public async Task OnPostPreviewImportAndImport_CallExpectedUseCases()
    {
        User currentUser = TestUsers.Create();
        Guid accountId = Guid.NewGuid();
        SaveBankStatementImportRequest importRequest = new() { AccountId = accountId };
        FormFile file = new(new MemoryStream([1, 2, 3]), 0, 3, "file", "statement.pdf");

        Dependencies dependencies = new();
        dependencies.MoneyMovementsPageUseCase
            .Setup(useCase => useCase.PreviewImportAsync(
                It.Is<PreviewBankStatementImportPageRequest>(request =>
                    request.BankId == "tbank" &&
                    request.AccountId == accountId &&
                    request.FileName == "statement.pdf" &&
                    request.FileLength == 3 &&
                    request.FileStream != null),
                currentUser.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<BankStatementImportPreviewDto>.Ok(new BankStatementImportPreviewDto { BankId = "tbank", AccountId = accountId }));
        dependencies.MoneyMovementsPageUseCase
            .Setup(useCase => useCase.ImportAsync(importRequest, currentUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<BankStatementImportResultDto>.Ok(new BankStatementImportResultDto { CreatedCount = 1 }));

        MoneyMovementsModel model = dependencies.CreateModel(currentUser);

        BankStatementImportPreviewDto preview = JsonResultAssert.Data<BankStatementImportPreviewDto>(
            await model.OnPostPreviewImportAsync("tbank", accountId, file, CancellationToken.None));
        BankStatementImportResultDto importResult = JsonResultAssert.Data<BankStatementImportResultDto>(
            await model.OnPostImportAsync(importRequest, CancellationToken.None));

        Assert.Equal("tbank", preview.BankId);
        Assert.Equal(1, importResult.CreatedCount);
    }

    private sealed class Dependencies
    {
        public Mock<IMoneyMovementsPageUseCase> MoneyMovementsPageUseCase { get; } = new(MockBehavior.Strict);

        public MoneyMovementsModel CreateModel(User currentUser)
        {
            return new MoneyMovementsModel(MoneyMovementsPageUseCase.Object)
            {
                CurrentUser = currentUser
            };
        }
    }
}
