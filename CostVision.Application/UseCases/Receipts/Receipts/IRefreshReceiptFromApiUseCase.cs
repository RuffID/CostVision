using CostVision.Application.Models.Responses.Results;
using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.Receipts;

namespace CostVision.Application.UseCases.Receipts.Receipts
{
    public interface IRefreshReceiptFromApiUseCase
    {
        Task<ServiceResult<Receipt>> ExecuteAsync(Guid receiptId, User currentUser, CancellationToken ct);
    }
}
