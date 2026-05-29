using CostVision.Infrastructure.Services.Helpers;
using Xunit;

namespace CostVision.Infrastructure.UnitTests.Services.Helpers;

public class HasherTests
{
    [Fact]
    public void Verify_ReturnsTrueForOriginalPassword()
    {
        Hasher hasher = new();

        string hash = hasher.Hash("secret");

        Assert.True(hasher.Verify("secret", hash));
    }

    [Fact]
    public void Verify_ReturnsFalseForWrongPassword()
    {
        Hasher hasher = new();

        string hash = hasher.Hash("secret");

        Assert.False(hasher.Verify("wrong", hash));
    }

    [Fact]
    public void Hash_UsesDifferentSaltForSamePassword()
    {
        Hasher hasher = new();

        string firstHash = hasher.Hash("secret");
        string secondHash = hasher.Hash("secret");

        Assert.NotEqual(firstHash, secondHash);
        Assert.True(hasher.Verify("secret", firstHash));
        Assert.True(hasher.Verify("secret", secondHash));
    }

    [Fact]
    public void HashAndVerify_HandleEmptyPassword()
    {
        Hasher hasher = new();

        string hash = hasher.Hash(string.Empty);

        Assert.True(hasher.Verify(string.Empty, hash));
        Assert.False(hasher.Verify("not empty", hash));
    }
}
