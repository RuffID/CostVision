using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.Receipts.Receipts.Refresh;
using CostVision.Domain.Models.Receipts;
using Microsoft.Extensions.Logging;

namespace CostVision.Application.UseCases.Receipts.Receipts
{
    public class RefreshReceiptsWithoutItemsUseCase(IUnitOfWork unitOfWork, IReceiptRefreshWorkflow receiptRefreshWorkflow, ILogger<RefreshReceiptsWithoutItemsUseCase> logger) : IRefreshReceiptsWithoutItemsUseCase
    {
        public async Task ExecuteAsync(CancellationToken ct)
        {
            List<Receipt> receipts = await unitOfWork.Receipt.GetWithoutItemsAsync(ct);

            int countUpdatedReceipts = 0;
            foreach (Receipt receipt in receipts)
            {
                await Task.Delay(1000, ct);
                ServiceResult<Receipt> result = await receiptRefreshWorkflow.RefreshAsync(receipt, ct);
                if (result.Success)
                    countUpdatedReceipts++;
            }

            if (countUpdatedReceipts > 0)
                logger.LogInformation("[Method:{MethodName}] Refreshed {Count} receipts without items from external API.", nameof(ExecuteAsync), countUpdatedReceipts);
        }
    }
}
