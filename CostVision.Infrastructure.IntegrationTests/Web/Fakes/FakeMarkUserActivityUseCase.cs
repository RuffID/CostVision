using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.Authorize.Users;

namespace CostVision.Infrastructure.IntegrationTests.Web.Fakes;

public sealed class FakeMarkUserActivityUseCase : IMarkUserActivityUseCase
{
    public ServiceResult<bool> Result { get; set; } = ServiceResult<bool>.Ok(true);

    public Guid? LastUserId { get; private set; }

    public Task<ServiceResult<bool>> ExecuteAsync(Guid userId, CancellationToken ct)
    {
        LastUserId = userId;
        return Task.FromResult(Result);
    }
}
