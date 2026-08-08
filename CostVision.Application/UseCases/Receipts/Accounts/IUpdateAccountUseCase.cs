using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Domain.Models.Receipts;

namespace CostVision.Application.UseCases.Receipts.Accounts
{
    public interface IUpdateAccountUseCase
    {
        Task<ServiceResult<UserAccountViewModel>> ExecuteAsync(Guid ownerUserId, UpdateAccountRequest request, CancellationToken ct);
    }
}
