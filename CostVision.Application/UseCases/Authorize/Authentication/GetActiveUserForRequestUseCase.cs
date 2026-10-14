using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Authorization;
using CostVision.Domain.Models.Authorization;
using Microsoft.EntityFrameworkCore;

namespace CostVision.Application.UseCases.Authorize.Authentication
{
    /// <summary>
    /// Загружает активного пользователя для обработанного запроса.
    /// </summary>
    public class GetActiveUserForRequestUseCase(IUserRepository userRepository) : IGetActiveUserForRequestUseCase
    {
        public async Task<User?> ExecuteAsync(Guid userId, CancellationToken ct)
        {
            User? user = await userRepository.GetItemByIdAsync(
                userId,
                asNoTracking: true,
                include: query => query.Include(user => user.Roles),
                ct: ct);
            return user is { IsActive: true } ? user : null;
        }
    }
}
