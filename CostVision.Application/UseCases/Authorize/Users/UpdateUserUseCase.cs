using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.Service.Authorize;
using CostVision.Domain.Models.Authorization;
using CostVision.Application.Models.Requests.Authorize;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.Authorize.Users.Helpers;

namespace CostVision.Application.UseCases.Authorize.Users
{
    public class UpdateUserUseCase(IUnitOfWork unitOfWork, IPasswordHasher passwordHasher) : IUpdateUserUseCase
    {
        public async Task<ServiceResult> ExecuteAsync(UserUpsertRequest request, CancellationToken ct)
        {
            if (!request.Id.HasValue)
                return ServiceResult.Fail(400, "Некорректный id пользователя.");

            ServiceResult<List<Guid>> validationResult = UserUpsertRequestValidator.ValidateUpsertRequest(request, requirePassword: false);
            if (!validationResult.Success || validationResult.Data == null)
                return ServiceResult.Fail(validationResult.Error!.StatusCode, validationResult.Error.Message);

            User? user = await unitOfWork.User.GetByIdWithRolesAsync(request.Id.Value, asNoTracking: false, ct);
            if (user == null)
                return ServiceResult.Fail(404, "Пользователь не найден.");

            string normalizedLogin = request.Login.Trim().ToUpperInvariant();
            if (!string.Equals(user.Login, request.Login.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                User? conflictUser = await unitOfWork.User.GetByNormalizedLoginAsync(normalizedLogin, asNoTracking: true, ct);
                if (conflictUser != null && conflictUser.Id != user.Id)
                    return ServiceResult.Fail(409, "Пользователь с таким логином уже существует.");
            }

            user.Login = request.Login.Trim();
            user.Name = request.Name.Trim();

            if (!string.IsNullOrWhiteSpace(request.Password))
                user.PasswordHash = passwordHasher.Hash(request.Password.Trim());

            List<Guid> desiredRoleIds = validationResult.Data;
            List<Guid> currentRoleIds = user.UserRoles.Select(userRole => userRole.RoleId).ToList();
            List<Guid> roleIdsToRemove = currentRoleIds.Except(desiredRoleIds).ToList();
            List<Guid> roleIdsToAdd = desiredRoleIds.Except(currentRoleIds).ToList();

            foreach (Guid roleId in roleIdsToRemove)
            {
                List<UserRole> items = user.UserRoles.Where(userRole => userRole.RoleId == roleId).ToList();
                foreach (UserRole item in items)
                    user.UserRoles.Remove(item);
            }

            foreach (Guid roleId in roleIdsToAdd)
            {
                user.UserRoles.Add(new UserRole
                {
                    UserId = user.Id,
                    RoleId = roleId
                });
            }

            await unitOfWork.SaveChangesAsync(ct);

            return ServiceResult.Ok();
        }
    }
}
