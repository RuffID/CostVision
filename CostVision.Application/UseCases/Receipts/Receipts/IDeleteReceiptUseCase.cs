using CostVision.Application.Models.Responses.Results;
using CostVision.Domain.Models.Authorization;

namespace CostVision.Application.UseCases.Receipts.Receipts
{
    public interface IDeleteReceiptUseCase
    {
        Task<ServiceResult> ExecuteAsync(Guid receiptId, User currentUser, CancellationToken ct);
    }
}
