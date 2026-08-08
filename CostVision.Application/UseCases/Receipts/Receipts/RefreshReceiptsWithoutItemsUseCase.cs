using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.Receipts.Receipts.Refresh;
using CostVision.Domain.Models.Receipts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CostVision.Application.UseCases.Receipts.Receipts
{
    public class RefreshReceiptsWithoutItemsUseCase(IUnitOfWork unitOfWork, IReceiptRefreshWorkflow receiptRefreshWorkflow, ILogger<RefreshReceiptsWithoutItemsUseCase> logger) : IRefreshReceiptsWithoutItemsUseCase
    {
        public async Task ExecuteAsync(CancellationToken ct)
        {
            List<Receipt> receipts = await unitOfWork.Receipt.GetItemsByPredicateAsync(
                receipt => !receipt.Items.Any(),
                include: query => query.Include(receipt => receipt.Items),
                ct: ct);

            int countUpdatedReceipts = 0;
            foreach (Receipt receipt in receipts)
            {
                await Task.Delay(1000, ct);
                ServiceResult<Receipt> result = await receiptRefreshWorkflow.RefreshAsync(receipt, ct);
                if (result.Success)
                {
                    countUpdatedReceipts++;
                    continue;
                }

                logger.LogWarning(
                    "[Method:{MethodName}] Failed to refresh receipt {ReceiptId} without items from external API. ErrorType: {ErrorType}. Error: {ErrorMessage}",
                    nameof(ExecuteAsync),
                    receipt.Id,
                    result.Error.Type,
                    result.Error.Message);
            }

            if (countUpdatedReceipts > 0)
                logger.LogInformation("[Method:{MethodName}] Refreshed {Count} receipts without items from external API.", nameof(ExecuteAsync), countUpdatedReceipts);
        }
    }
}
