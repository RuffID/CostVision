using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;

namespace CostVision.Application.UseCases.Receipts.Accounts
{
    public interface IGetUserAccountsForReceiptCreationUseCase
    {
        Task<ServiceResult<List<UserAccountViewModel>>> ExecuteAsync(Guid userId, CancellationToken ct);
    }
}
