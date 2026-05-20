using CostVision.Application.Models.Responses.Results;
using CostVision.Domain.Models.Authorization;

namespace CostVision.Application.UseCases.Receipts.Receipts
{
    public interface IDeleteReceiptUseCase
    {
        Task<ServiceResult<bool>> ExecuteAsync(Guid receiptId, User currentUser, CancellationToken ct);
    }
}
