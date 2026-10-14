using CostVision.Web.Extensions;
using Xunit;

namespace CostVision.Web.UnitTests.Web.Extensions;

public class StartupCancellationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StartupToken_CancelsFromCallerOrSignal_WithoutStartingHost(bool signal)
    {
        // Arrange
        using CancellationTokenSource caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using StartupCancellation startup = new(caller.Token);

        // Act
        if (signal)
        {
            // Моделируем доставку сигнала; настоящий OS signal/процесс не запускается.
            startup.Cancel();
        }
        else
        {
            caller.Cancel();
        }

        // Assert
        Assert.True(startup.Token.IsCancellationRequested);
    }

    [Fact]
    public void DisposedStartupSignalSubscription_DoesNotCancelOrReuseDisposedSource()
    {
        // Arrange
        StartupCancellation startup = new(TestContext.Current.CancellationToken);
        CancellationToken token = startup.Token;

        // Act
        startup.Dispose();
        startup.Cancel();
        startup.Dispose();

        // Assert
        Assert.False(token.IsCancellationRequested);
    }
}
