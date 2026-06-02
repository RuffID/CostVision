using CostVision.Application.Models.Dtos.Receipts;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.MoneyMovements;
using CostVision.Application.UseCases.Receipts.Accounts;
using CostVision.Application.UseCases.Receipts.Receipts;
using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.Receipts;
using CostVision.Web.Pages;
using CostVision.Web.UnitTests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace CostVision.Web.UnitTests.Pages;

public class ReceiptsModelTests
{
    [Fact]
    public async Task OnGetReceiptListAsync_CallsReceiptListPageUseCase()
    {
        User currentUser = TestUsers.Create();
        DateTime date = new(2026, 5, 1);
        ReceiptDto receipt = new() { Id = Guid.NewGuid(), DateTime = date, TotalSum = 100 };

        Dependencies dependencies = new();
        receipt.AvailableMoneyMovementCount = 2;
        dependencies.GetReceiptListPageUseCase
            .Setup(useCase => useCase.ExecuteAsync(
                currentUser,
                It.Is<GetReceiptListRequest>(request => request.DateFrom == date && request.DateTo == date),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<List<ReceiptDto>>.Ok([receipt]));

        ReceiptsModel model = dependencies.CreateModel(currentUser);

        JsonResult json = await model.OnGetReceiptListAsync(date, date, CancellationToken.None);

        List<ReceiptDto> data = JsonResultAssert.Data<List<ReceiptDto>>(json);
        Assert.Equal(2, data.Single().AvailableMoneyMovementCount);
    }

    [Fact]
    public async Task OnPostOpenReceiptAsync_ReturnsReceiptDtoWithItems()
    {
        User currentUser = TestUsers.Create();
        Guid receiptId = Guid.NewGuid();
        Receipt receipt = new()
        {
            Id = receiptId,
            CreatedByUserId = currentUser.Id,
            RetailPlace = "Shop",
            DateTime = new DateTime(2026, 5, 1),
            Items =
            [
                new ReceiptItem
                {
                    Product = new Product { Name = "Milk" },
                    Quantity = 2,
                    Price = 10,
                    Sum = 20
                }
            ]
        };

        Dependencies dependencies = new();
        dependencies.GetReceiptWithItemsUseCase
            .Setup(useCase => useCase.ExecuteAsync(receiptId, currentUser, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<Receipt>.Ok(receipt));

        ReceiptsModel model = dependencies.CreateModel(currentUser);

        JsonResult json = await model.OnPostOpenReceiptAsync(new OpenReceiptRequest { ReceiptId = receiptId }, CancellationToken.None);

        ReceiptDto data = JsonResultAssert.Data<ReceiptDto>(json);
        Assert.Equal(receiptId, data.Id);
        Assert.Equal("Milk", data.Items.Single().Name);
    }

    [Fact]
    public async Task OnPostDeleteReceiptAsync_CallsDeleteUseCase()
    {
        User currentUser = TestUsers.Create();
        Guid receiptId = Guid.NewGuid();

        Dependencies dependencies = new();
        dependencies.DeleteReceiptUseCase
            .Setup(useCase => useCase.ExecuteAsync(receiptId, currentUser, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<bool>.Ok(true));

        ReceiptsModel model = dependencies.CreateModel(currentUser);

        JsonResult json = await model.OnPostDeleteReceiptAsync(new DeleteReceiptRequest { ReceiptId = receiptId }, CancellationToken.None);

        JsonResultAssert.Data<bool>(json);
        dependencies.DeleteReceiptUseCase.VerifyAll();
    }

    [Fact]
    public async Task OnPostMoveReceiptToAccountAsync_CallsMoveUseCase()
    {
        User currentUser = TestUsers.Create();
        MoveReceiptToAccountRequest request = new()
        {
            SourceAccountId = Guid.NewGuid(),
            TargetAccountId = Guid.NewGuid(),
            ReceiptId = Guid.NewGuid()
        };

        Dependencies dependencies = new();
        dependencies.MoveReceiptToAccountUseCase
            .Setup(useCase => useCase.ExecuteAsync(request.SourceAccountId, request.TargetAccountId, request.ReceiptId, currentUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<bool>.Ok(true));

        ReceiptsModel model = dependencies.CreateModel(currentUser);

        JsonResult json = await model.OnPostMoveReceiptToAccountAsync(request, CancellationToken.None);

        JsonResultAssert.Data<bool>(json);
        dependencies.MoveReceiptToAccountUseCase.VerifyAll();
    }

    [Fact]
    public async Task OnPostRefreshReceiptAsync_CallsRefreshUseCase()
    {
        User currentUser = TestUsers.Create();
        Guid receiptId = Guid.NewGuid();
        Receipt receipt = new()
        {
            Id = receiptId,
            CreatedByUserId = currentUser.Id,
            DateTime = new DateTime(2026, 5, 1)
        };

        Dependencies dependencies = new();
        dependencies.RefreshReceiptFromApiUseCase
            .Setup(useCase => useCase.ExecuteAsync(receiptId, currentUser, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<Receipt>.Ok(receipt));

        ReceiptsModel model = dependencies.CreateModel(currentUser);

        JsonResult json = await model.OnPostRefreshReceiptAsync(new RefreshReceiptRequest { ReceiptId = receiptId }, CancellationToken.None);

        ReceiptDto data = JsonResultAssert.Data<ReceiptDto>(json);
        Assert.Equal(receiptId, data.Id);
        dependencies.RefreshReceiptFromApiUseCase.VerifyAll();
    }

    private sealed class Dependencies
    {
        public Mock<IGetUserAccountsUseCase> GetUserAccountsUseCase { get; } = new(MockBehavior.Strict);
        public Mock<IGetReceiptListPageUseCase> GetReceiptListPageUseCase { get; } = new(MockBehavior.Strict);
        public Mock<IGetReceiptWithItemsUseCase> GetReceiptWithItemsUseCase { get; } = new(MockBehavior.Strict);
        public Mock<IRefreshReceiptFromApiUseCase> RefreshReceiptFromApiUseCase { get; } = new(MockBehavior.Strict);
        public Mock<IDeleteReceiptUseCase> DeleteReceiptUseCase { get; } = new(MockBehavior.Strict);
        public Mock<IMoveReceiptToAccountUseCase> MoveReceiptToAccountUseCase { get; } = new(MockBehavior.Strict);
        public Mock<IRemoveReceiptFromAccountUseCase> RemoveReceiptFromAccountUseCase { get; } = new(MockBehavior.Strict);
        public Mock<IGetLinkedReceiptMoneyMovementsUseCase> GetLinkedReceiptMoneyMovementsUseCase { get; } = new(MockBehavior.Strict);
        public Mock<IGetReceiptMoneyMovementCandidatesUseCase> GetReceiptMoneyMovementCandidatesUseCase { get; } = new(MockBehavior.Strict);
        public Mock<ILinkMoneyMovementReceiptUseCase> LinkMoneyMovementReceiptUseCase { get; } = new(MockBehavior.Strict);
        public Mock<IUnlinkMoneyMovementReceiptUseCase> UnlinkMoneyMovementReceiptUseCase { get; } = new(MockBehavior.Strict);

        public ReceiptsModel CreateModel(User currentUser)
        {
            return new ReceiptsModel(
                GetUserAccountsUseCase.Object,
                GetReceiptListPageUseCase.Object,
                GetReceiptWithItemsUseCase.Object,
                RefreshReceiptFromApiUseCase.Object,
                DeleteReceiptUseCase.Object,
                MoveReceiptToAccountUseCase.Object,
                RemoveReceiptFromAccountUseCase.Object,
                GetLinkedReceiptMoneyMovementsUseCase.Object,
                GetReceiptMoneyMovementCandidatesUseCase.Object,
                LinkMoneyMovementReceiptUseCase.Object,
                UnlinkMoneyMovementReceiptUseCase.Object)
            {
                CurrentUser = currentUser
            };
        }
    }
}
