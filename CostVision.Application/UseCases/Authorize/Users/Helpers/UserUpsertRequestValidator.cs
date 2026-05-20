using CostVision.Application.Models.Requests.Authorize;
using CostVision.Application.Models.Responses.Results;

namespace CostVision.Application.UseCases.Authorize.Users.Helpers
{
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
