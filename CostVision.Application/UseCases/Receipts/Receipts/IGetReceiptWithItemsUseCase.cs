using CostVision.Application.Models.Responses.Results;
using CostVision.Application.Models.Dtos.Receipts;
using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.Receipts;

namespace CostVision.Application.UseCases.Receipts.Receipts
{
    public interface IGetReceiptWithItemsUseCase
    {
        Task<ServiceResult<ReceiptDto>> ExecuteAsync(Guid receiptId, User currentUser, CancellationToken ct);
    }
}
