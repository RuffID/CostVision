using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.Service.Authorize;
using CostVision.Domain.Models.Authorization;
using CostVision.Application.Models.Requests.Authorize;
using CostVision.Application.Models.Responses.Results;

namespace CostVision.Application.UseCases.Authorize.Authentication
{
    public class AuthenticateUserUseCase(IUnitOfWork unitOfWork, IPasswordHasher passwordHasher) : IAuthenticateUserUseCase
    {
        public async Task<ServiceResult<User>> ExecuteAsync(LoginRequest request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.Login) || string.IsNullOrWhiteSpace(request.Password))
                return ServiceResult<User>.Fail(400, "Укажи логин и пароль.");

            User? user = await unitOfWork.User.GetByLoginWithRolesAsync(request.Login, asNoTracking: false, ct);

            if (user == null || !passwordHasher.Verify(request.Password, user.PasswordHash))
                return ServiceResult<User>.Fail(401, "Неверный логин или пароль.");

            user.MarkLogin(DateTime.UtcNow);
            await unitOfWork.SaveChangesAsync(ct);

            return ServiceResult<User>.Ok(user);
        }
    }
}
