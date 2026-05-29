using CostVision.Application.Models.Responses.Results;
using CostVision.Domain.Models.Authorization;
using CostVision.Application.Models.Dtos.Receipts;

namespace CostVision.Application.UseCases.Receipts.Receipts
{
    public interface IGetReceiptListUseCase
    {
        Task<ServiceResult<List<ReceiptDto>>> ExecuteAsync(User currentUser, DateTime dateFrom, DateTime dateTo, CancellationToken ct);
    }
}
