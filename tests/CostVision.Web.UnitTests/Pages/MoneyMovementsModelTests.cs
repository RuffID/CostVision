using CostVision.Application.Models.Dtos.MoneyMovements;
using CostVision.Application.Models.Dtos;
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
    public async Task OnGetListAndCandidates_PreserveExplicitPagingFiltersAndFullTotals()
    {
        // Arrange
        User user = TestUsers.Create();
        DateTime date = new(2026, 1, 15);
        Guid accountId = Guid.NewGuid();
        GetMoneyMovementReceiptCandidatesRequest request = new()
        {
            MoneyMovementId = Guid.NewGuid(), Page = 2, PageSize = 1
        };
        MoneyMovementListDto expectedList = new()
        {
            Items = [new MoneyMovementDto { Amount = 1m }], Page = 3, PageSize = 5,
            TotalCount = 30, IncomeSum = 500m, ExpenseSum = 1000m
        };
        PagedListDto<MoneyMovementReceiptDto> expectedCandidates = PagedListDto<MoneyMovementReceiptDto>.Create(
            [new MoneyMovementReceiptDto(), new MoneyMovementReceiptDto()], 4, 2, 1);
        Dependencies dependencies = new();
        dependencies.MoneyMovementsPageUseCase.Setup(useCase => useCase.GetListAsync(user.Id, date, date, accountId,
            It.IsAny<CancellationToken>(), 3, 5, "coffee", "withReceipts"))
            .ReturnsAsync(ServiceResult<MoneyMovementListDto>.Ok(expectedList));
        dependencies.MoneyMovementsPageUseCase.Setup(useCase => useCase.GetReceiptCandidatesAsync(request, user.Id,
            It.IsAny<CancellationToken>())).ReturnsAsync(ServiceResult<PagedListDto<MoneyMovementReceiptDto>>.Ok(expectedCandidates));
        MoneyMovementsModel model = dependencies.CreateModel(user);

        // Act
        MoneyMovementListDto list = JsonResultAssert.Data<MoneyMovementListDto>(
            await model.OnGetListAsync(date, date, accountId, CancellationToken.None, 3, 5, "coffee", "withReceipts"));
        PagedListDto<MoneyMovementReceiptDto> candidates = JsonResultAssert.Data<PagedListDto<MoneyMovementReceiptDto>>(
            await model.OnGetReceiptCandidatesAsync(request, CancellationToken.None));

        // Assert
        Assert.Same(expectedList, list);
        Assert.Equal(30, list.TotalCount);
        Assert.Equal(1000m, list.ExpenseSum);
        Assert.Equal(4, candidates.TotalCount);
        Assert.Equal(2, candidates.Items.Count);
        Assert.Equal(1, candidates.PageSize);
        dependencies.MoneyMovementsPageUseCase.VerifyAll();
    }

    [Fact]
    public async Task OnGetListAsync_CallsListUseCase()
    {
        User currentUser = TestUsers.Create();
        DateTime dateFrom = new(2026, 5, 1);
        DateTime dateTo = new(2026, 5, 2);
        Guid accountId = Guid.NewGuid();

        Dependencies dependencies = new();
        dependencies.MoneyMovementsPageUseCase
            .Setup(useCase => useCase.GetListAsync(currentUser.Id, dateFrom, dateTo, accountId, It.IsAny<CancellationToken>(), 1, 20, null, null))
            .ReturnsAsync(ServiceResult<MoneyMovementListDto>.Ok(new MoneyMovementListDto { Items = [new MoneyMovementDto { Id = Guid.NewGuid() }] }));

        MoneyMovementsModel model = dependencies.CreateModel(currentUser);

        JsonResult json = await model.OnGetListAsync(dateFrom, dateTo, accountId, CancellationToken.None);

        Assert.Single(JsonResultAssert.Data<MoneyMovementListDto>(json).Items);
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
            .ReturnsAsync(ServiceResult.Ok());
        dependencies.MoneyMovementsPageUseCase
            .Setup(useCase => useCase.MoveToAccountAsync(moveRequest, currentUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult.Ok());

        MoneyMovementsModel model = dependencies.CreateModel(currentUser);

        JsonResultAssert.Data<MoneyMovementDto>(await model.OnPostCreateAsync(createRequest, CancellationToken.None));
        JsonResultAssert.Success(await model.OnPostDeleteAsync(deleteRequest, CancellationToken.None));
        JsonResultAssert.Success(await model.OnPostMoveToAccountAsync(moveRequest, CancellationToken.None));
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
            .ReturnsAsync(ServiceResult.Ok());
        dependencies.MoneyMovementsPageUseCase
            .Setup(useCase => useCase.UnlinkReceiptAsync(unlinkRequest, currentUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult.Ok());

        MoneyMovementsModel model = dependencies.CreateModel(currentUser);

        JsonResultAssert.Success(await model.OnPostLinkReceiptAsync(linkRequest, CancellationToken.None));
        JsonResultAssert.Success(await model.OnPostUnlinkReceiptAsync(unlinkRequest, CancellationToken.None));
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
