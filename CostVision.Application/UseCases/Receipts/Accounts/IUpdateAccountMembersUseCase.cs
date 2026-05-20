using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;

namespace CostVision.Application.UseCases.Receipts.Accounts
{
    public interface IUpdateAccountMembersUseCase
    {
        Task<ServiceResult<bool>> ExecuteAsync(Guid accountId, Guid ownerUserId, IReadOnlyCollection<UpdateAccountMemberRequest> members, CancellationToken ct);
    }
}
