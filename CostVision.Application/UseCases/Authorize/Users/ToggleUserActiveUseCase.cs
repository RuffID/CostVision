using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Domain.Models.Authorization;
using CostVision.Application.Models.Responses.Results;

namespace CostVision.Application.UseCases.Authorize.Users
{
    public class ToggleUserActiveUseCase(IUnitOfWork unitOfWork) : IToggleUserActiveUseCase
    {
        public async Task<ServiceResult<bool>> ExecuteAsync(Guid id, CancellationToken ct)
        {
            User? user = await unitOfWork.User.GetItemByIdAsync(id, ct: ct);
            if (user == null)
                return ServiceResult<bool>.Fail(ServiceErrorType.NotFound, "Пользователь не найден");

            if (user.IsActive)
                user.Deactivate();
            else
                user.Activate();

            await unitOfWork.SaveChangesAsync(ct);

            return ServiceResult<bool>.Ok(user.IsActive);
        }
    }
}
