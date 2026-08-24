using CostVision.Domain.Models.Receipts;
using Xunit;

namespace CostVision.Application.UnitTests.UseCases.Receipts.Products;

public class ProductStoreTests
{
    [Fact]
    public void ProductTryCreate_TrimsDetails()
    {
        bool success = Product.TryCreate(
            "  Milk  ",
            "  MILK  ",
            out Product? product,
            out string? error);

        Assert.True(success, error);
        Assert.Equal("Milk", product!.Name);
        Assert.Equal("MILK", product.NormalizedName);
    }

    [Fact]
    public void ProductTryUpdateDetails_DoesNotChangeState_WhenDataIsInvalid()
    {
        Product.TryCreate("Milk", "MILK", out Product? product, out _);

        bool success = product!.TryUpdateDetails(
            "Changed",
            string.Empty,
            out string? error);

        Assert.False(success);
        Assert.NotNull(error);
        Assert.Equal("Milk", product.Name);
        Assert.Equal("MILK", product.NormalizedName);
    }

    [Fact]
    public void ProductTryUpdateAdaptiveName_DoesNotChangeState_WhenValueIsTooLong()
    {
        Product.TryCreate("Milk", "MILK", out Product? product, out _);
        product!.TryUpdateAdaptiveName("Milk 1L", out _);

        bool success = product.TryUpdateAdaptiveName(
            new string('a', Product.MAX_ADAPTIVE_NAME_LENGTH + 1),
            out string? error);

        Assert.False(success);
        Assert.NotNull(error);
        Assert.Equal("Milk 1L", product.AdaptiveName);
    }

    [Fact]
    public void StoreTryCreate_AllowsAddressWithoutName()
    {
        bool success = Store.TryCreate(
            null,
            string.Empty,
            "  Main street  ",
            "  MAINSTREET  ",
            out Store? store,
            out string? error);

        Assert.True(success, error);
        Assert.Equal(string.Empty, store!.Name);
        Assert.Equal("Main street", store.Address);
        Assert.Equal("MAINSTREET", store.NormalizedAddress);
    }

    [Fact]
    public void StoreTryUpdateDetails_DoesNotChangeState_WhenDataIsInvalid()
    {
        Store.TryCreate("Shop", "SHOP", "Address", "ADDRESS", out Store? store, out _);

        bool success = store!.TryUpdateDetails(
            "Changed",
            string.Empty,
            "New address",
            "NEWADDRESS",
            out string? error);

        Assert.False(success);
        Assert.NotNull(error);
        Assert.Equal("Shop", store.Name);
        Assert.Equal("SHOP", store.NormalizedName);
        Assert.Equal("Address", store.Address);
        Assert.Equal("ADDRESS", store.NormalizedAddress);
    }
}
