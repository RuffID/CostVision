using CostVision.Application.Models.Responses.Results;

namespace CostVision.Application.UseCases.Receipts.Accounts
{
    public interface IValidateReceiptCreationAccessUseCase
    {
        Task<ServiceResult> ExecuteAsync(Guid accountId, Guid userId, CancellationToken ct);
    }
}
