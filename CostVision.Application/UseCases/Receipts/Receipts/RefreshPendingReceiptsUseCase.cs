using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.Receipts.Receipts.Refresh;
using CostVision.Domain.Models.Enums.Receipts;
using CostVision.Domain.Models.Receipts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CostVision.Application.UseCases.Receipts.Receipts
{
    public class RefreshPendingReceiptsUseCase(
        IServiceScopeFactory scopeFactory,
        ILogger<RefreshPendingReceiptsUseCase> logger,
        TimeProvider timeProvider) : IRefreshPendingReceiptsUseCase
    {
        public const int BATCH_SIZE = 32;

        public async Task ExecuteAsync(CancellationToken ct)
        {
            DateTime runAtUtc = timeProvider.GetUtcNow().UtcDateTime;
            DateTime? cursorCreatedAtUtc = null;
            Guid cursorId = Guid.Empty;
            int updatedCount = 0;
            bool hasProcessedDocument = false;
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                List<Receipt> batch;
                await using (AsyncServiceScope scope = scopeFactory.CreateAsyncScope())
                {
                    IUnitOfWork unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                    batch = await unitOfWork.Receipt.GetItemsByPredicateAsync(
                        receipt => receipt.RefreshStatus == ReceiptRefreshStatus.Pending &&
                            receipt.RefreshAttemptCount < ReceiptRefreshPolicy.MAX_ATTEMPTS &&
                            (!receipt.NextRefreshAttemptAtUtc.HasValue || receipt.NextRefreshAttemptAtUtc <= runAtUtc) &&
                            !receipt.Items.Any() && receipt.CreatedAtUtc <= runAtUtc &&
                            (!cursorCreatedAtUtc.HasValue || receipt.CreatedAtUtc > cursorCreatedAtUtc.Value ||
                                (receipt.CreatedAtUtc == cursorCreatedAtUtc.Value && receipt.Id.CompareTo(cursorId) > 0)),
                        take: BATCH_SIZE,
                        asNoTracking: true,
                        include: query => query.OrderBy(receipt => receipt.CreatedAtUtc).ThenBy(receipt => receipt.Id),
                        ct: ct);
                }

                if (batch.Count == 0)
                {
                    break;
                }

                foreach (Receipt snapshot in batch)
                {
                    ct.ThrowIfCancellationRequested();
                    if (hasProcessedDocument)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1), timeProvider, ct);
                    }

                    hasProcessedDocument = true;
                    cursorCreatedAtUtc = snapshot.CreatedAtUtc;
                    cursorId = snapshot.Id;
                    DateTime attemptedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
                    DateTime nextAttemptAtUtc = ReceiptRefreshPolicy.GetNextDailyRunUtc(timeProvider);
                    try
                    {
                        ServiceResult<Receipt> result;
                        await using (AsyncServiceScope scope = scopeFactory.CreateAsyncScope())
                        {
                            IReceiptRefreshWorkflow workflow = scope.ServiceProvider.GetRequiredService<IReceiptRefreshWorkflow>();
                            result = await workflow.RefreshScheduledAsync(snapshot, attemptedAtUtc, nextAttemptAtUtc, ct);
                            if (result.Success)
                            {
                                updatedCount++;
                            }
                            else
                            {
                                logger.LogWarning("Scheduled receipt refresh failed. ReceiptId: {ReceiptId}. ErrorType: {ErrorType}. Error: {Error}",
                                    snapshot.Id, result.Error.Type, result.Error.Message);
                            }
                        }

                        if (!result.Success && result.Error.Type == ServiceErrorType.Conflict)
                        {
                            await RegisterFailureInNewScopeAsync(snapshot, attemptedAtUtc, nextAttemptAtUtc, ct);
                        }
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        logger.LogError(exception, "Unexpected receipt refresh failure. ReceiptId: {ReceiptId}. Scope discarded.", snapshot.Id);
                        await RegisterFailureInNewScopeAsync(snapshot, attemptedAtUtc, nextAttemptAtUtc, ct);
                    }
                }
            }

            if (updatedCount > 0)
            {
                logger.LogInformation("Refreshed {Count} pending receipts from external API.", updatedCount);
            }
        }

        private async Task RegisterFailureInNewScopeAsync(
            Receipt snapshot, DateTime attemptedAtUtc, DateTime nextAttemptAtUtc, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();

                IReceiptRefreshWorkflow workflow = scope.ServiceProvider.GetRequiredService<IReceiptRefreshWorkflow>();
                ServiceResult<Receipt> result = await workflow.RegisterScheduledFailureAsync(snapshot,
                    "Не удалось обновить чек. Подробности ошибки сохранены в журнале приложения.",
                    attemptedAtUtc, nextAttemptAtUtc, ct);
                if (result.Success)
                {
                    throw new InvalidOperationException("Регистрация отказа не должна возвращать успешное обновление чека.");
                }

                if (result.Error.Type != ServiceErrorType.ExternalService)
                {
                    logger.LogWarning("Receipt refresh retry-state rejected. ReceiptId: {ReceiptId}. ErrorType: {ErrorType}",
                        snapshot.Id, result.Error.Type);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to persist receipt retry-state in a new scope. ReceiptId: {ReceiptId}", snapshot.Id);
            }
        }
    }
}
