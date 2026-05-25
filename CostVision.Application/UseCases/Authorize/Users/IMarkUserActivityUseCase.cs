using CostVision.Application.Models.Responses.Results;

namespace CostVision.Application.UseCases.Authorize.Users
{
    public interface IMarkUserActivityUseCase
    {
        Task<ServiceResult<bool>> ExecuteAsync(Guid userId, CancellationToken ct);
    }
}
