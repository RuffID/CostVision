using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.Service.Authorize;
using CostVision.Application.Models.Dtos.Authorization;
using CostVision.Domain.Models.Authorization;
using CostVision.Application.Models.Requests.Authorize;
using CostVision.Application.Models.Responses.Results;
using Microsoft.EntityFrameworkCore;

namespace CostVision.Application.UseCases.Authorize.Authentication
{
    public class AuthenticateUserUseCase(IUnitOfWork unitOfWork, IPasswordHasher passwordHasher) : IAuthenticateUserUseCase
    {
        public async Task<ServiceResult<AuthenticatedUserDto>> ExecuteAsync(LoginRequest request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.Login) || string.IsNullOrWhiteSpace(request.Password))
                return ServiceResult<AuthenticatedUserDto>.Fail(ServiceErrorType.Validation, "Укажи логин и пароль.");

            User? user = await unitOfWork.User.GetItemByPredicateAsync(
                user => user.Login == request.Login,
                asNoTracking: false,
                include: query => query.Include(user => user.Roles),
                ct: ct);

            if (user == null || !passwordHasher.Verify(request.Password, user.PasswordHash))
                return ServiceResult<AuthenticatedUserDto>.Fail(ServiceErrorType.Unauthorized, "Неверный логин или пароль.");

            if (!user.IsActive)
                return ServiceResult<AuthenticatedUserDto>.Fail(ServiceErrorType.Forbidden, "Пользователь заблокирован.");

            if (!user.TryMarkLogin(DateTime.UtcNow, out string? error))
                return ServiceResult<AuthenticatedUserDto>.Fail(ServiceErrorType.Validation, error!);

            await unitOfWork.SaveChangesAsync(ct);

            return ServiceResult<AuthenticatedUserDto>.Ok(new AuthenticatedUserDto
            {
                Id = user.Id,
                Name = user.Name,
                Roles = user.Roles.Select(role => role.RoleType).ToList()
            });
        }
    }
}
