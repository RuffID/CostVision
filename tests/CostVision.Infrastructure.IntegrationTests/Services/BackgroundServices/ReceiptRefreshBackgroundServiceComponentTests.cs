using CostVision.Application.UseCases.Receipts.Receipts;
using CostVision.Infrastructure.IntegrationTests.Web.Helpers;
using CostVision.Infrastructure.Services.BackgroundServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CostVision.Infrastructure.IntegrationTests.Services.BackgroundServices;

public class ReceiptRefreshBackgroundServiceComponentTests
{
    [Fact]
    public async Task StartAsync_ScheduledRunArrives_ExecutesRefreshWorkflow()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        SequenceScheduler scheduler = new(1);
        FakeRefreshPendingReceiptsUseCase refreshUseCase = new(_ => Task.CompletedTask);
        await using ServiceProvider serviceProvider = CreateServiceProvider(refreshUseCase);
        ListLoggerProvider loggerProvider = new();
        using ILoggerFactory loggerFactory = CreateLoggerFactory(loggerProvider);
        ReceiptRefreshBackgroundService service = new(
            serviceProvider,
            loggerFactory.CreateLogger<ReceiptRefreshBackgroundService>(),
            scheduler);

        await service.StartAsync(ct);
        await refreshUseCase.Executed.Task.WaitAsync(ct);
        await service.StopAsync(ct);

        Assert.Equal(1, refreshUseCase.ExecuteCount);
        Assert.Contains(loggerProvider.Entries, entry =>
            entry.LogLevel == LogLevel.Information &&
            entry.Message.Contains("The update of receipts from the API has been launched."));
    }

    [Fact]
    public async Task StartAsync_RefreshWorkflowThrows_LogsErrorAndContinuesLifecycle()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        SequenceScheduler scheduler = new(2);
        TaskCompletionSource secondExecution = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int executionCount = 0;
        FakeRefreshPendingReceiptsUseCase refreshUseCase = new(_ =>
        {
            executionCount++;

            if (executionCount == 1)
                throw new InvalidOperationException("Refresh failed.");

            secondExecution.TrySetResult();
            return Task.CompletedTask;
        });
        await using ServiceProvider serviceProvider = CreateServiceProvider(refreshUseCase);
        ListLoggerProvider loggerProvider = new();
        using ILoggerFactory loggerFactory = CreateLoggerFactory(loggerProvider);
        ReceiptRefreshBackgroundService service = new(
            serviceProvider,
            loggerFactory.CreateLogger<ReceiptRefreshBackgroundService>(),
            scheduler);

        await service.StartAsync(ct);
        await secondExecution.Task.WaitAsync(ct);
        await service.StopAsync(ct);

        Assert.Equal(2, refreshUseCase.ExecuteCount);
        Assert.Contains(loggerProvider.Entries, entry =>
            entry.LogLevel == LogLevel.Error &&
            entry.Message.Contains("An error occurred while refreshing receipts."));
    }

    [Fact]
    public async Task StopAsync_WhenWaitingForNextRun_CancelsHostedService()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        SequenceScheduler scheduler = new(0);
        FakeRefreshPendingReceiptsUseCase refreshUseCase = new(_ => Task.CompletedTask);
        await using ServiceProvider serviceProvider = CreateServiceProvider(refreshUseCase);
        using ILoggerFactory loggerFactory = LoggerFactory.Create(static _ => { });
        ReceiptRefreshBackgroundService service = new(
            serviceProvider,
            loggerFactory.CreateLogger<ReceiptRefreshBackgroundService>(),
            scheduler);

        await service.StartAsync(ct);
        await scheduler.WaitingStarted.Task.WaitAsync(ct);
        await service.StopAsync(ct);

        Assert.Equal(0, refreshUseCase.ExecuteCount);
    }

    private static ServiceProvider CreateServiceProvider(IRefreshPendingReceiptsUseCase refreshUseCase)
    {
        ServiceCollection services = new();
        services.AddScoped(_ => refreshUseCase);

        return services.BuildServiceProvider();
    }

    private static ILoggerFactory CreateLoggerFactory(ListLoggerProvider loggerProvider)
    {
        return LoggerFactory.Create(builder => builder.AddProvider(loggerProvider));
    }

    private sealed class SequenceScheduler(int immediateRuns) : IReceiptRefreshBackgroundScheduler
    {
        private int _remainingImmediateRuns = immediateRuns;

        public TaskCompletionSource WaitingStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task WaitForNextRunAsync(CancellationToken ct)
        {
            if (_remainingImmediateRuns > 0)
            {
                _remainingImmediateRuns--;
                return Task.CompletedTask;
            }

            WaitingStarted.TrySetResult();
            return Task.Delay(Timeout.InfiniteTimeSpan, ct);
        }
    }

    private sealed class FakeRefreshPendingReceiptsUseCase(Func<CancellationToken, Task> execute) : IRefreshPendingReceiptsUseCase
    {
        public int ExecuteCount { get; private set; }

        public TaskCompletionSource Executed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task ExecuteAsync(CancellationToken ct)
        {
            ExecuteCount++;
            Executed.TrySetResult();
            await execute(ct);
        }
    }
}
