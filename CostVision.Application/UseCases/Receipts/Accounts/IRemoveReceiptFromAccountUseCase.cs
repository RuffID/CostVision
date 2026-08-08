using CostVision.Application.Models.Responses.Results;

namespace CostVision.Application.UseCases.Receipts.Accounts
{
    public interface IRemoveReceiptFromAccountUseCase
    {
        Task<ServiceResult> ExecuteAsync(Guid accountId, Guid receiptId, Guid currentUserId, CancellationToken ct);
    }
}
