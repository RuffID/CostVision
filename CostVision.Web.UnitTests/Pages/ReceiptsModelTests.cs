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
            .ReturnsAsync(ServiceResult<ReceiptListDto>.Ok(new ReceiptListDto { Items = [receipt] }));

        ReceiptsModel model = dependencies.CreateModel(currentUser);

        JsonResult json = await model.OnGetReceiptListAsync(new GetReceiptListRequest { DateFrom = date, DateTo = date }, CancellationToken.None);

        ReceiptListDto data = JsonResultAssert.Data<ReceiptListDto>(json);
        Assert.Equal(2, data.Items.Single().AvailableMoneyMovementCount);
    }

    [Fact]
    public async Task OnPostOpenReceiptAsync_ReturnsReceiptDtoWithItems()
    {
        User currentUser = TestUsers.Create();
        Guid receiptId = Guid.NewGuid();
        Store.TryCreate("Shop", "SHOP", null, null, out Store? store, out string? storeError);
        Assert.NotNull(store);
        Assert.Null(storeError);
        Receipt receipt = CreateReceipt(receiptId, currentUser.Id, new DateTime(2026, 5, 1), store);
        Assert.True(Product.TryCreate("Milk", "MILK", out Product? product, out string? productError), productError);
        Assert.True(ReceiptItem.TryCreate(10, 2, 20, 0, default, default, default, product!, null, out ReceiptItem? item, out string? itemError), itemError);
        Assert.True(receipt.TryAddItem(item!, out itemError), itemError);

        Dependencies dependencies = new();
        dependencies.GetReceiptWithItemsUseCase
            .Setup(useCase => useCase.ExecuteAsync(receiptId, currentUser, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<ReceiptDto>.Ok(new ReceiptDto
            {
                Id = receiptId,
                Items = [new ReceiptItemDto { Name = "Milk", Quantity = 2, Price = 10, Sum = 20 }]
            }));

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
            .ReturnsAsync(ServiceResult.Ok());

        ReceiptsModel model = dependencies.CreateModel(currentUser);

        JsonResult json = await model.OnPostDeleteReceiptAsync(new DeleteReceiptRequest { ReceiptId = receiptId }, CancellationToken.None);

        JsonResultAssert.Success(json);
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
            .ReturnsAsync(ServiceResult.Ok());

        ReceiptsModel model = dependencies.CreateModel(currentUser);

        JsonResult json = await model.OnPostMoveReceiptToAccountAsync(request, CancellationToken.None);

        JsonResultAssert.Success(json);
        dependencies.MoveReceiptToAccountUseCase.VerifyAll();
    }

    [Fact]
    public async Task OnPostRefreshReceiptAsync_CallsRefreshUseCase()
    {
        User currentUser = TestUsers.Create();
        Guid receiptId = Guid.NewGuid();
        Receipt receipt = CreateReceipt(receiptId, currentUser.Id, new DateTime(2026, 5, 1));

        Dependencies dependencies = new();
        dependencies.RefreshReceiptFromApiUseCase
            .Setup(useCase => useCase.ExecuteAsync(receiptId, currentUser, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<ReceiptDto>.Ok(new ReceiptDto { Id = receipt.Id }));

        ReceiptsModel model = dependencies.CreateModel(currentUser);

        JsonResult json = await model.OnPostRefreshReceiptAsync(new RefreshReceiptRequest { ReceiptId = receiptId }, CancellationToken.None);

        ReceiptDto data = JsonResultAssert.Data<ReceiptDto>(json);
        Assert.Equal(receiptId, data.Id);
        dependencies.RefreshReceiptFromApiUseCase.VerifyAll();
    }

    private static Receipt CreateReceipt(Guid receiptId, Guid createdByUserId, DateTime dateTime, Store? store = null)
    {
        bool isCreated = Receipt.TryCreate(
            "fn",
            "fd",
            "fp",
            dateTime,
            CostVision.Domain.Models.Enums.Receipts.ReceiptOperationType.Income,
            100,
            createdByUserId,
            new DateTime(2026, 1, 1),
            out Receipt? receipt,
            out string? error);
        Assert.True(isCreated, error);
        receipt!.Id = receiptId;
        receipt.AssignStore(store);
        return receipt;
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
