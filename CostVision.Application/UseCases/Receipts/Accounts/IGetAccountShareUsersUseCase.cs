using CostVision.Application.Models.Dtos.Receipts;
using CostVision.Application.Models.Responses.Results;

namespace CostVision.Application.UseCases.Receipts.Accounts
{
    public interface IGetAccountShareUsersUseCase
    {
        Task<ServiceResult<List<AccountShareUserDto>>> ExecuteAsync(Guid accountId, Guid ownerUserId, CancellationToken ct);
    }
}
