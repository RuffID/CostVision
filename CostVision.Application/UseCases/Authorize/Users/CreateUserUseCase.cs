using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.Service.Authorize;
using CostVision.Domain.Models.Authorization;
using CostVision.Application.Models.Requests.Authorize;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.Authorize.Users.Helpers;

namespace CostVision.Application.UseCases.Authorize.Users
{
    public class CreateUserUseCase(IUnitOfWork unitOfWork, IPasswordHasher passwordHasher) : ICreateUserUseCase
    {
        public async Task<ServiceResult> ExecuteAsync(UserUpsertRequest request, CancellationToken ct)
        {
            ServiceResult<List<Guid>> validationResult = UserUpsertRequestValidator.ValidateUpsertRequest(request, requirePassword: true);
            if (!validationResult.Success || validationResult.Data == null)
                return ServiceResult.Fail(validationResult.Error!.StatusCode, validationResult.Error.Message);

            string normalizedLogin = request.Login.Trim().ToUpperInvariant();
            User? existingUser = await unitOfWork.User.GetItemByPredicateAsync(
                user => user.Login.ToUpper() == normalizedLogin,
                asNoTracking: true,
                ct: ct);
            if (existingUser != null)
                return ServiceResult.Fail(409, "Пользователь с таким логином уже существует");

            User user = new()
            {
                Login = request.Login.Trim(),
                Name = request.Name.Trim(),
                PasswordHash = passwordHasher.Hash(request.Password!.Trim()),
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
                LastLoginAtUtc = null
            };

            foreach (Guid roleId in validationResult.Data)
            {
                user.UserRoles.Add(new UserRole
                {
                    User = user,
                    RoleId = roleId
                });
            }

            unitOfWork.User.Create(user);
            await unitOfWork.SaveChangesAsync(ct);

            return ServiceResult.Ok();
        }
    }
}
