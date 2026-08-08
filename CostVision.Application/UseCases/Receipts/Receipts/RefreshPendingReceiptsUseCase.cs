using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.Receipts.Receipts.Refresh;
using CostVision.Domain.Models.Enums.Receipts;
using CostVision.Domain.Models.Receipts;
using Microsoft.Extensions.Logging;

namespace CostVision.Application.UseCases.Receipts.Receipts
{
    public class RefreshPendingReceiptsUseCase(
        IUnitOfWork unitOfWork,
        IReceiptRefreshWorkflow receiptRefreshWorkflow,
        ILogger<RefreshPendingReceiptsUseCase> logger,
        TimeProvider timeProvider) : IRefreshPendingReceiptsUseCase
    {
        public async Task ExecuteAsync(CancellationToken ct)
        {
            DateTime attemptedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
            DateTime nextAttemptAtUtc = ReceiptRefreshPolicy.GetNextDailyRunUtc(timeProvider);
            List<Receipt> receipts = await unitOfWork.Receipt.GetItemsByPredicateAsync(
                receipt => receipt.RefreshStatus == ReceiptRefreshStatus.Pending &&
                           receipt.RefreshAttemptCount < ReceiptRefreshPolicy.MAX_ATTEMPTS &&
                           (!receipt.NextRefreshAttemptAtUtc.HasValue || receipt.NextRefreshAttemptAtUtc <= attemptedAtUtc) &&
                           !receipt.Items.Any(),
                ct: ct);

            int updatedCount = 0;
            foreach (Receipt receipt in receipts)
            {
                ServiceResult<Receipt> result = await receiptRefreshWorkflow.RefreshScheduledAsync(
                    receipt,
                    attemptedAtUtc,
                    nextAttemptAtUtc,
                    ct);

                if (result.Success)
                {
                    updatedCount++;
                }
                else
                {
                    logger.LogWarning(
                        "[Method:{MethodName}] Scheduled receipt refresh failed. ReceiptId: {ReceiptId}. Attempt: {Attempt}/{MaxAttempts}. Status: {Status}. ErrorType: {ErrorType}. Error: {ErrorMessage}",
                        nameof(ExecuteAsync),
                        receipt.Id,
                        receipt.RefreshAttemptCount,
                        ReceiptRefreshPolicy.MAX_ATTEMPTS,
                        receipt.RefreshStatus,
                        result.Error.Type,
                        result.Error.Message);
                }

                if (!ReferenceEquals(receipt, receipts[^1]))
                    await Task.Delay(TimeSpan.FromSeconds(1), ct);
            }

            if (updatedCount > 0)
            {
                logger.LogInformation(
                    "[Method:{MethodName}] Refreshed {Count} pending receipts from external API.",
                    nameof(ExecuteAsync),
                    updatedCount);
            }
        }
    }
}
