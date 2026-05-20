using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Domain.Models.Authorization;

namespace CostVision.Application.UseCases.Receipts.Receipts.Helpers
{
    internal static class UserActivityUpdater
    {
        public static async Task MarkReceiptActivityAsync(IUnitOfWork unitOfWork, Guid userId, CancellationToken ct)
        {
            User? user = await unitOfWork.User.GetItemByIdAsync(userId, asNoTracking: false, ct: ct);
            if (user == null)
                throw new InvalidOperationException("Не удалось обновить последнюю активность пользователя: пользователь не найден.");

            user.MarkActivity(DateTime.UtcNow);
        }
    }
}
