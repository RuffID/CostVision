using CostVision.Application.Models.Dtos.Receipts;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.Receipts.Products;
using CostVision.Domain.Models.Authorization;
using CostVision.Web.Pages;
using CostVision.Web.UnitTests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace CostVision.Web.UnitTests.Pages;

public class ProductsModelTests
{
    [Fact]
    public async Task OnGetListAsync_CallsProductListUseCase()
    {
        User currentUser = TestUsers.Create();
        GetProductListRequest request = new() { Search = "milk" };

        Mock<IGetProductListUseCase> getProductListUseCase = new(MockBehavior.Strict);
        getProductListUseCase
            .Setup(useCase => useCase.ExecuteAsync(request, currentUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<ProductListDto>.Ok(new ProductListDto { Items = [new ProductListItemDto { Name = "Milk" }] }));
        Mock<IUpdateProductAdaptiveNameUseCase> updateProductAdaptiveNameUseCase = new(MockBehavior.Strict);

        ProductsModel model = new(getProductListUseCase.Object, updateProductAdaptiveNameUseCase.Object)
        {
            CurrentUser = currentUser
        };

        JsonResult json = await model.OnGetListAsync(request, CancellationToken.None);

        ProductListDto data = JsonResultAssert.Data<ProductListDto>(json);
        Assert.Equal("Milk", data.Items.Single().Name);
    }

    [Fact]
    public async Task OnPostUpdateAdaptiveNameAsync_CallsUpdateUseCase()
    {
        UpdateProductAdaptiveNameRequest request = new() { ProductId = Guid.NewGuid(), AdaptiveName = "Milk 1L" };

        Mock<IGetProductListUseCase> getProductListUseCase = new(MockBehavior.Strict);
        Mock<IUpdateProductAdaptiveNameUseCase> updateProductAdaptiveNameUseCase = new(MockBehavior.Strict);
        updateProductAdaptiveNameUseCase
            .Setup(useCase => useCase.ExecuteAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<bool>.Ok(true));

        ProductsModel model = new(getProductListUseCase.Object, updateProductAdaptiveNameUseCase.Object)
        {
            CurrentUser = TestUsers.Create()
        };

        JsonResult json = await model.OnPostUpdateAdaptiveNameAsync(request, CancellationToken.None);

        JsonResultAssert.Data<bool>(json);
        updateProductAdaptiveNameUseCase.VerifyAll();
    }
}
