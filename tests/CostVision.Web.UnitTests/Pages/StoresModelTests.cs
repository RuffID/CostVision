using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.MoneyMovements;
using CostVision.Application.UseCases.Receipts.Accounts;
using CostVision.Application.UseCases.Receipts.Receipts;
using CostVision.Application.UseCases.Receipts.Stores;
using CostVision.Domain.Models.Authorization;
using CostVision.Web.Pages;
using CostVision.Web.UnitTests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace CostVision.Web.UnitTests.Pages;

public class StoresModelTests
{
    [Fact]
    public async Task UpdateAdaptiveName_PassesCurrentUserAndMapsAccessDenial()
    {
        // Arrange
        User user = TestUsers.Create();
        UpdateStoreAdaptiveNameRequest request = new() { StoreId = Guid.NewGuid(), AdaptiveName = "Common" };
        Mock<IUpdateStoreAdaptiveNameUseCase> update = new(MockBehavior.Strict);
        update.Setup(useCase => useCase.ExecuteAsync(request, user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult.Fail(ServiceErrorType.Forbidden, "Нет доступа."));
        StoresModel model = new(
            Mock.Of<IGetStoreListUseCase>(), Mock.Of<IGetStoreReceiptListUseCase>(), update.Object,
            Mock.Of<IGetUserAccountsUseCase>(), Mock.Of<IGetReceiptWithItemsUseCase>(),
            Mock.Of<IRefreshReceiptFromApiUseCase>(), Mock.Of<IMoveReceiptToAccountUseCase>(),
            Mock.Of<IRemoveReceiptFromAccountUseCase>(), Mock.Of<IGetLinkedReceiptMoneyMovementsUseCase>(),
            Mock.Of<IGetReceiptMoneyMovementCandidatesUseCase>(), Mock.Of<ILinkMoneyMovementReceiptUseCase>(),
            Mock.Of<IUnlinkMoneyMovementReceiptUseCase>())
        {
            CurrentUser = user
        };

        // Act
        JsonResult result = await model.OnPostUpdateAdaptiveNameAsync(request, CancellationToken.None);

        // Assert
        Assert.Equal(403, result.StatusCode);
        update.VerifyAll();
    }
}
