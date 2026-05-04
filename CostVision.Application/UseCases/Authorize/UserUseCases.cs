using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.Service.Authorize;
using CostVision.Domain.Models.Authorization;
using CostVision.Application.Models.Dtos.Authorization;
using CostVision.Application.Models.Requests.Authorize;
using CostVision.Application.Models.Responses.Results;

namespace CostVision.Application.UseCases.Authorize
{
    public interface IGetUserListUseCase
    {
        Task<ServiceResult<List<UserListItemDto>>> ExecuteAsync(bool includeInactive, CancellationToken ct);
    }

    public interface IGetUserUseCase
    {
        Task<ServiceResult<UserEditDto>> ExecuteAsync(Guid id, CancellationToken ct);
    }

    public interface ICreateUserUseCase
    {
        Task<ServiceResult> ExecuteAsync(UserUpsertRequest request, CancellationToken ct);
    }

    public interface IUpdateUserUseCase
    {
        Task<ServiceResult> ExecuteAsync(UserUpsertRequest request, CancellationToken ct);
    }

    public interface IToggleUserActiveUseCase
    {
        Task<ServiceResult<bool>> ExecuteAsync(Guid id, CancellationToken ct);
    }

    public class GetUserListUseCase(IUnitOfWork unitOfWork) : IGetUserListUseCase
    {
        public async Task<ServiceResult<List<UserListItemDto>>> ExecuteAsync(bool includeInactive, CancellationToken ct)
        {
            List<User> users = await unitOfWork.User.GetListWithRolesAsync(includeInactive, ct);

            List<UserListItemDto> items = users
                .OrderBy(user => user.Name)
                .Select(user => new UserListItemDto
                {
                    Id = user.Id,
                    Name = user.Name,
                    Login = user.Login,
                    IsActive = user.IsActive,
                    CreatedAtUtc = user.CreatedAtUtc,
                    LastLoginAtUtc = user.LastLoginAtUtc
                })
                .ToList();

            return ServiceResult<List<UserListItemDto>>.Ok(items);
        }
    }

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

    public class CreateUserUseCase(IUnitOfWork unitOfWork, IPasswordHasher passwordHasher) : ICreateUserUseCase
    {
        public async Task<ServiceResult> ExecuteAsync(UserUpsertRequest request, CancellationToken ct)
        {
            ServiceResult<List<Guid>> validationResult = UserUpsertRequestValidator.ValidateUpsertRequest(request, requirePassword: true);
            if (!validationResult.Success || validationResult.Data == null)
                return ServiceResult.Fail(validationResult.Error!.StatusCode, validationResult.Error.Message);

            string normalizedLogin = request.Login.Trim().ToUpperInvariant();
            User? existingUser = await unitOfWork.User.GetByNormalizedLoginAsync(normalizedLogin, asNoTracking: true, ct);
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

    public class ToggleUserActiveUseCase(IUnitOfWork unitOfWork) : IToggleUserActiveUseCase
    {
        public async Task<ServiceResult<bool>> ExecuteAsync(Guid id, CancellationToken ct)
        {
            User? user = await unitOfWork.User.GetItemByIdAsync(id, ct: ct);
            if (user == null)
                return ServiceResult<bool>.Fail(404, "Пользователь не найден");

            if (user.IsActive)
                user.Deactivate();
            else
                user.Activate();

            await unitOfWork.SaveChangesAsync(ct);

            return ServiceResult<bool>.Ok(user.IsActive);
        }
    }

    internal static class UserUpsertRequestValidator
    {
        public static ServiceResult<List<Guid>> ValidateUpsertRequest(UserUpsertRequest request, bool requirePassword)
        {
            if (string.IsNullOrWhiteSpace(request.Login))
                return ServiceResult<List<Guid>>.Fail(400, "Логин обязателен.");

            if (string.IsNullOrWhiteSpace(request.Name))
                return ServiceResult<List<Guid>>.Fail(400, "Имя обязательно.");

            if (requirePassword && string.IsNullOrWhiteSpace(request.Password))
                return ServiceResult<List<Guid>>.Fail(400, "Пароль обязателен.");

            List<Guid> roleIds = request.RoleIds.Where(roleId => roleId != Guid.Empty).Distinct().ToList();
            if (roleIds.Count == 0)
                return ServiceResult<List<Guid>>.Fail(400, "Роль обязательна.");

            return ServiceResult<List<Guid>>.Ok(roleIds);
        }
    }
}
