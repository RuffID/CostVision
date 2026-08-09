using CostVision.Domain.Models.Authorization;

namespace CostVision.Application.UseCases.Authorize.Authentication
{
    /// <summary>
    /// Загружает активного пользователя для обработанного запроса.
    /// </summary>
    public interface IGetActiveUserForRequestUseCase
    {
        Task<User?> ExecuteAsync(Guid userId, CancellationToken ct);
    }
}
