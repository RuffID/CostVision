using CostVision.Application.Models.Requests.Authorize;
using CostVision.Application.Models.Responses.Results;

namespace CostVision.Application.UseCases.Authorize.Users
{
    public interface IUpdateUserUseCase
    {
        Task<ServiceResult> ExecuteAsync(UserUpsertRequest request, CancellationToken ct);
    }
}
