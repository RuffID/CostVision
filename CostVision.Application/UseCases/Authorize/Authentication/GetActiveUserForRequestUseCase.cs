using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Domain.Models.Authorization;
using Microsoft.EntityFrameworkCore;

namespace CostVision.Application.UseCases.Authorize.Authentication
{
    /// <summary>
    /// Загружает активного пользователя для обработанного запроса.
    /// </summary>
    public class GetActiveUserForRequestUseCase(IUnitOfWork unitOfWork) : IGetActiveUserForRequestUseCase
    {
        public async Task<User?> ExecuteAsync(Guid userId, CancellationToken ct)
        {
            User? user = await unitOfWork.User.GetItemByIdAsync(
                userId,
                asNoTracking: true,
                include: query => query.Include(user => user.Roles),
                ct: ct);
            return user is { IsActive: true } ? user : null;
        }
    }
}
