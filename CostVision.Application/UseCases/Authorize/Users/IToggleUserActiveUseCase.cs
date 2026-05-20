using CostVision.Application.Models.Responses.Results;

namespace CostVision.Application.UseCases.Authorize.Users
{
    public interface IToggleUserActiveUseCase
    {
        Task<ServiceResult<bool>> ExecuteAsync(Guid id, CancellationToken ct);
    }
}
