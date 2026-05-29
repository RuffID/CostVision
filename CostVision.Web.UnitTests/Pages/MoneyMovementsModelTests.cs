using CostVision.Application.Models.Dtos.MoneyMovements;
using CostVision.Application.Models.Requests.MoneyMovements;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.MoneyMovements;
using CostVision.Application.UseCases.Receipts.Receipts;
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
        dependencies.GetMoneyMovementListUseCase
            .Setup(useCase => useCase.ExecuteAsync(currentUser.Id, dateFrom, dateTo, accountId, It.IsAny<CancellationToken>()))
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
        dependencies.CreateMoneyMovementUseCase
            .Setup(useCase => useCase.ExecuteAsync(createRequest, currentUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<MoneyMovementDto>.Ok(new MoneyMovementDto { Id = Guid.NewGuid() }));
        dependencies.DeleteMoneyMovementUseCase
            .Setup(useCase => useCase.ExecuteAsync(deleteRequest, currentUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<bool>.Ok(true));
        dependencies.MoveMoneyMovementToAccountUseCase
            .Setup(useCase => useCase.ExecuteAsync(moveRequest, currentUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<bool>.Ok(true));

        MoneyMovementsModel model = dependencies.CreateModel(currentUser);

        JsonResultAssert.Data<MoneyMovementDto>(await model.OnPostCreateAsync(createRequest, CancellationToken.None));
        JsonResultAssert.Data<bool>(await model.OnPostDeleteAsync(deleteRequest, CancellationToken.None));
        JsonResultAssert.Data<bool>(await model.OnPostMoveToAccountAsync(moveRequest, CancellationToken.None));
        dependencies.CreateMoneyMovementUseCase.VerifyAll();
        dependencies.DeleteMoneyMovementUseCase.VerifyAll();
        dependencies.MoveMoneyMovementToAccountUseCase.VerifyAll();
    }

    [Fact]
    public async Task OnPostLinkAndUnlinkReceipt_CallExpectedUseCases()
    {
        User currentUser = TestUsers.Create();
        LinkMoneyMovementReceiptRequest linkRequest = new() { MoneyMovementId = Guid.NewGuid(), ReceiptId = Guid.NewGuid() };
        UnlinkMoneyMovementReceiptRequest unlinkRequest = new() { MoneyMovementId = linkRequest.MoneyMovementId, ReceiptId = linkRequest.ReceiptId };

        Dependencies dependencies = new();
        dependencies.LinkMoneyMovementReceiptUseCase
            .Setup(useCase => useCase.ExecuteAsync(linkRequest, currentUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<bool>.Ok(true));
        dependencies.UnlinkMoneyMovementReceiptUseCase
            .Setup(useCase => useCase.ExecuteAsync(unlinkRequest, currentUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<bool>.Ok(true));

        MoneyMovementsModel model = dependencies.CreateModel(currentUser);

        JsonResultAssert.Data<bool>(await model.OnPostLinkReceiptAsync(linkRequest, CancellationToken.None));
        JsonResultAssert.Data<bool>(await model.OnPostUnlinkReceiptAsync(unlinkRequest, CancellationToken.None));
        dependencies.LinkMoneyMovementReceiptUseCase.VerifyAll();
        dependencies.UnlinkMoneyMovementReceiptUseCase.VerifyAll();
    }

    [Fact]
    public async Task OnPostPreviewImportAndImport_CallExpectedUseCases()
    {
        User currentUser = TestUsers.Create();
        Guid accountId = Guid.NewGuid();
        SaveBankStatementImportRequest importRequest = new() { AccountId = accountId };
        FormFile file = new(new MemoryStream([1, 2, 3]), 0, 3, "file", "statement.pdf");

        Dependencies dependencies = new();
        dependencies.PreviewBankStatementImportUseCase
            .Setup(useCase => useCase.ExecuteAsync("tbank", accountId, "statement.pdf", It.IsAny<Stream>(), currentUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<BankStatementImportPreviewDto>.Ok(new BankStatementImportPreviewDto { BankId = "tbank", AccountId = accountId }));
        dependencies.ImportMoneyMovementsUseCase
            .Setup(useCase => useCase.ExecuteAsync(importRequest, currentUser.Id, It.IsAny<CancellationToken>()))
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
        public Mock<IGetMoneyMovementAccountsUseCase> GetMoneyMovementAccountsUseCase { get; } = new(MockBehavior.Strict);
        public Mock<IGetMoneyMovementListUseCase> GetMoneyMovementListUseCase { get; } = new(MockBehavior.Strict);
        public Mock<ICreateMoneyMovementUseCase> CreateMoneyMovementUseCase { get; } = new(MockBehavior.Strict);
        public Mock<IMoveMoneyMovementToAccountUseCase> MoveMoneyMovementToAccountUseCase { get; } = new(MockBehavior.Strict);
        public Mock<IDeleteMoneyMovementUseCase> DeleteMoneyMovementUseCase { get; } = new(MockBehavior.Strict);
        public Mock<IUpdateMoneyMovementCommentUseCase> UpdateMoneyMovementCommentUseCase { get; } = new(MockBehavior.Strict);
        public Mock<IGetBankStatementImportBanksUseCase> GetBankStatementImportBanksUseCase { get; } = new(MockBehavior.Strict);
        public Mock<IPreviewBankStatementImportUseCase> PreviewBankStatementImportUseCase { get; } = new(MockBehavior.Strict);
        public Mock<IImportMoneyMovementsUseCase> ImportMoneyMovementsUseCase { get; } = new(MockBehavior.Strict);
        public Mock<IGetLinkedMoneyMovementReceiptsUseCase> GetLinkedMoneyMovementReceiptsUseCase { get; } = new(MockBehavior.Strict);
        public Mock<IGetMoneyMovementReceiptCandidatesUseCase> GetMoneyMovementReceiptCandidatesUseCase { get; } = new(MockBehavior.Strict);
        public Mock<ILinkMoneyMovementReceiptUseCase> LinkMoneyMovementReceiptUseCase { get; } = new(MockBehavior.Strict);
        public Mock<IUnlinkMoneyMovementReceiptUseCase> UnlinkMoneyMovementReceiptUseCase { get; } = new(MockBehavior.Strict);
        public Mock<IGetReceiptWithItemsUseCase> GetReceiptWithItemsUseCase { get; } = new(MockBehavior.Strict);

        public MoneyMovementsModel CreateModel(User currentUser)
        {
            return new MoneyMovementsModel(
                GetMoneyMovementAccountsUseCase.Object,
                GetMoneyMovementListUseCase.Object,
                CreateMoneyMovementUseCase.Object,
                MoveMoneyMovementToAccountUseCase.Object,
                DeleteMoneyMovementUseCase.Object,
                UpdateMoneyMovementCommentUseCase.Object,
                GetBankStatementImportBanksUseCase.Object,
                PreviewBankStatementImportUseCase.Object,
                ImportMoneyMovementsUseCase.Object,
                GetLinkedMoneyMovementReceiptsUseCase.Object,
                GetMoneyMovementReceiptCandidatesUseCase.Object,
                LinkMoneyMovementReceiptUseCase.Object,
                UnlinkMoneyMovementReceiptUseCase.Object,
                GetReceiptWithItemsUseCase.Object)
            {
                CurrentUser = currentUser
            };
        }
    }
}
