using CostVision.Application.Models.Dtos.Receipts;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Domain.Models.Authorization;

namespace CostVision.Application.UseCases.Receipts.Receipts
{
    public interface IGetReceiptListPageUseCase
    {
        Task<ServiceResult<ReceiptListDto>> ExecuteAsync(User currentUser, GetReceiptListRequest request, CancellationToken ct);
    }
}
