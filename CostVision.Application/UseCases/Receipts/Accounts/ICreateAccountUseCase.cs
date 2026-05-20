using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Domain.Models.Receipts;

namespace CostVision.Application.UseCases.Receipts.Accounts
{
    public interface ICreateAccountUseCase
    {
        Task<ServiceResult<Account>> ExecuteAsync(Guid ownerUserId, CreateAccountRequest request, CancellationToken ct);
    }
}
