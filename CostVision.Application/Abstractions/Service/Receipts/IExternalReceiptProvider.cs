using CostVision.Domain.Models.Receipts;
using CostVision.Application.Models.Responses.Results;

namespace CostVision.Application.Abstractions.Service.Receipts
{
    public interface IExternalReceiptProvider
    {
        Task<ServiceResult<Receipt>> GetReceiptAsync(Receipt receipt, CancellationToken ct);
    }
}
