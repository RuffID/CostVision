using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Domain.Models.Authorization;
using CostVision.Application.Models.Dtos.Authorization;
using CostVision.Application.Models.Responses.Results;

namespace CostVision.Application.UseCases.Authorize.Users
{
    public class GetUserUseCase(IUnitOfWork unitOfWork) : IGetUserUseCase
    {
        public async Task<ServiceResult<UserEditDto>> ExecuteAsync(Guid id, CancellationToken ct)
        {
            User? user = await unitOfWork.User.GetByIdWithRolesAsync(id, asNoTracking: true, ct);
            if (user == null)
                return ServiceResult<UserEditDto>.Fail(404, "Пользователь не найден");

            UserEditDto dto = new()
            {
                Id = user.Id,
                Name = user.Name,
                Login = user.Login,
                RoleIds = user.UserRoles.Select(userRole => userRole.RoleId).ToList()
            };

            return ServiceResult<UserEditDto>.Ok(dto);
        }
    }
}
