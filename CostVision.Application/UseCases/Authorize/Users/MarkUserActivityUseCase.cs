using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Models.Responses.Results;
using CostVision.Domain.Models.Authorization;

namespace CostVision.Application.UseCases.Authorize.Users
{
    public class MarkUserActivityUseCase(IUnitOfWork unitOfWork) : IMarkUserActivityUseCase
    {
        public async Task<ServiceResult> ExecuteAsync(Guid userId, CancellationToken ct)
        {
            if (userId == Guid.Empty)
                return ServiceResult.Fail(ServiceErrorType.Validation, "Некорректный идентификатор пользователя.");

            User? user = await unitOfWork.User.GetItemByIdAsync(userId, asNoTracking: false, ct: ct);
            if (user == null || !user.IsActive)
                return ServiceResult.Fail(ServiceErrorType.NotFound, "Пользователь не найден или неактивен.");

            if (!user.TryMarkActivity(DateTime.UtcNow, out string? error))
                return ServiceResult.Fail(ServiceErrorType.Validation, error!);

            await unitOfWork.SaveChangesAsync(ct);

            return ServiceResult.Ok();
        }
    }
}
