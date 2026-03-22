using CostVision.Interfaces.DataBase.Repositories;
using CostVision.Interfaces.Service.Authorize;
using CostVision.Models.Authorization;
using CostVision.Models.Dtos.Authorization;
using CostVision.Models.Requests.Authorize;
using CostVision.Models.Responses.Results;
using CostVision.Services.Helpers;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace CostVision.Services.Authorize
{
    public class UserService(IUnitOfWork unitOfWork, Hasher hasher) : IUserService
    {
        public async Task<ServiceResult<List<UserListItemDto>>> GetUserListAsync(bool includeInactive, CancellationToken ct)
        {
            Expression<Func<User, bool>>? predicate = u => includeInactive || u.IsActive;

            List<User> users = await unitOfWork.User.GetItemsByPredicate(predicate: predicate, asNoTracking: true,
                include: q => q
                    .Include(u => u.UserRoles),
                ct: ct);

            List<UserListItemDto> dto = users
                .OrderBy(x => x.Name)
                .Select(x => new UserListItemDto
                {
                    Id = x.Id,
                    Name = x.Name,
                    Login = x.Login,
                    IsActive = x.IsActive,
                    CreatedAtUtc = x.CreatedAtUtc,
                    LastLoginAtUtc = x.LastLoginAtUtc
                })
                .ToList();

            return ServiceResult<List<UserListItemDto>>.Ok(dto);
        }

        public async Task<ServiceResult<UserEditDto>> GetUserAsync(Guid id, CancellationToken ct)
        {
            User? user = await unitOfWork.User.GetItemById(id, asNoTracking: true,
                include: q => q.Include(u => u.UserRoles),
                ct: ct);

            if (user == null)
                return ServiceResult<UserEditDto>.Fail(404, "Пользователь не найден");

            List<Guid> roleIds = user.UserRoles.Select(ur => ur.RoleId).ToList();

            UserEditDto dto = new ()
            {
                Id = user.Id,
                Name = user.Name,
                Login = user.Login,
                RoleIds = roleIds
            };

            return ServiceResult<UserEditDto>.Ok(dto);
        }

        public async Task<ServiceResult> CreateUserAsync(UserUpsertRequest dto, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(dto.Login))
            
                return ServiceResult.Fail(400, "Логин обязателен.");            

            if (string.IsNullOrWhiteSpace(dto.Name))            
                return ServiceResult.Fail(400, "Имя обязательно.");            

            if (string.IsNullOrWhiteSpace(dto.Password))            
                return ServiceResult.Fail(400, "Пароль обязателен.");
            
            if (dto.RoleIds.Count == 0)        
                return ServiceResult.Fail(400, "Роль обязательна.");

            User? existing = await unitOfWork.User.GetItemByPredicate(u => u.Login.Equals(dto.Login.ToLower(), StringComparison.CurrentCultureIgnoreCase), asNoTracking: true, ct: ct);

            if (existing != null)
                return ServiceResult.Fail(409, "Пользователь с таким логином уже существует");

            List<Guid> roleIds = dto.RoleIds.Where(x => x != Guid.Empty).ToList();

            User user = new ()
            {
                Login = dto.Login.Trim(),
                Name = dto.Name.Trim(),
                PasswordHash = hasher.Hash(dto.Password.Trim()),
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
                LastLoginAtUtc = null
            };

            foreach (Guid roleId in roleIds)
            {
                user.UserRoles.Add(new UserRole
                {
                    UserId = user.Id,
                    RoleId = roleId
                });
            }

            unitOfWork.User.Create(user);

            await unitOfWork.SaveChangesAsync(ct);
            return ServiceResult.Ok();
        }

        public async Task<ServiceResult> UpdateUserAsync(UserUpsertRequest dto, CancellationToken ct)
        {
            if (!dto.Id.HasValue)            
                return ServiceResult.Fail(400, "Некорректный id пользователя.");
            
            User? user = await unitOfWork.User.GetItemById(dto.Id.Value, include: q => q.Include(x => x.UserRoles), ct: ct);

            if (user == null)
                return ServiceResult.Fail(404, "Пользователь не найден.");

            if (string.IsNullOrWhiteSpace(dto.Login))
                return ServiceResult.Fail(400, "Логин обязателен.");

            if (string.IsNullOrWhiteSpace(dto.Name))
                return ServiceResult.Fail(400, "Имя обязательно.");

            if (dto.RoleIds.Count == 0)
                return ServiceResult.Fail(400, "Роль обязательна.");

            if (!string.Equals(user.Login, dto.Login, StringComparison.OrdinalIgnoreCase))
            {
                User? conflict = await unitOfWork.User.GetItemByPredicate(
                    u => u.Login.Equals(dto.Login.ToLower(), StringComparison.CurrentCultureIgnoreCase), 
                    asNoTracking: true, 
                    ct: ct);

                if (conflict != null && conflict.Id != user.Id)
                    return ServiceResult.Fail(409, "Пользователь с таким логином уже существует.");
            }

            user.Login = dto.Login.Trim();
            user.Name = dto.Name.Trim();

            if (!string.IsNullOrWhiteSpace(dto.Password))
                user.PasswordHash = hasher.Hash(dto.Password.Trim());

            List<Guid> desiredRoleIds = dto.RoleIds.Where(x => x != Guid.Empty).ToList();

            if (desiredRoleIds.Count == 0)
                return ServiceResult.Fail(400, "Роль обязательна.");

            List<Guid> currentRoleIds = user.UserRoles.Select(x => x.RoleId).ToList();

            List<Guid> roleIdsToRemove = currentRoleIds.Except(desiredRoleIds).ToList();
            List<Guid> roleIdsToAdd = desiredRoleIds.Except(currentRoleIds).ToList();

            foreach (Guid roleId in roleIdsToRemove)
            {
                List<UserRole> items = user.UserRoles.Where(x => x.RoleId == roleId).ToList();
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

        public async Task<ServiceResult<bool>> ToggleUserActiveAsync(Guid id, CancellationToken ct)
        {
            User? user = await unitOfWork.User.GetItemById(id, ct: ct);
            
            if (user == null)
                return ServiceResult<bool>.Fail(404, "Пользователь не найден");

            user.IsActive = !user.IsActive;

            await unitOfWork.SaveChangesAsync(ct);

            return ServiceResult<bool>.Ok(user.IsActive);
        }
    }
}
