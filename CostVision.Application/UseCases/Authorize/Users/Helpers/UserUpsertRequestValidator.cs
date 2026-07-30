using CostVision.Application.Models.Requests.Authorize;
using CostVision.Application.Models.Responses.Results;
using CostVision.Domain.Models.Authorization;

namespace CostVision.Application.UseCases.Authorize.Users.Helpers
{
    internal static class UserUpsertRequestValidator
    {
        private const int MIN_PASSWORD_LENGTH = 8;
        private const int MAX_PASSWORD_LENGTH = 128;

        public static ServiceResult<List<Guid>> ValidateUpsertRequest(UserUpsertRequest request, bool requirePassword)
        {
            if (string.IsNullOrWhiteSpace(request.Login))
                return ServiceResult<List<Guid>>.Fail(400, "Логин обязателен.");

            string login = request.Login.Trim();
            if (login.Length is < User.MIN_LOGIN_LENGTH or > User.MAX_LOGIN_LENGTH)
                return ServiceResult<List<Guid>>.Fail(400, $"Логин должен быть от {User.MIN_LOGIN_LENGTH} до {User.MAX_LOGIN_LENGTH} символов.");

            if (string.IsNullOrWhiteSpace(request.Name))
                return ServiceResult<List<Guid>>.Fail(400, "Имя обязательно.");

            string name = request.Name.Trim();
            if (name.Length is < User.MIN_NAME_LENGTH or > User.MAX_NAME_LENGTH)
                return ServiceResult<List<Guid>>.Fail(400, $"Имя должно быть от {User.MIN_NAME_LENGTH} до {User.MAX_NAME_LENGTH} символов.");

            if (requirePassword && string.IsNullOrWhiteSpace(request.Password))
                return ServiceResult<List<Guid>>.Fail(400, "Пароль обязателен.");

            if (!string.IsNullOrWhiteSpace(request.Password) && !IsPasswordValid(request.Password.Trim()))
                return ServiceResult<List<Guid>>.Fail(400, $"Пароль должен быть на английском языке, от {MIN_PASSWORD_LENGTH} до {MAX_PASSWORD_LENGTH} символов, содержать заглавную букву, цифру и спецсимвол.");

            List<Guid> roleIds = request.RoleIds.Where(roleId => roleId != Guid.Empty).Distinct().ToList();
            if (roleIds.Count == 0)
                return ServiceResult<List<Guid>>.Fail(400, "Роль обязательна.");

            return ServiceResult<List<Guid>>.Ok(roleIds);
        }

        private static bool IsPasswordValid(string password)
        {
            return password.Length is >= MIN_PASSWORD_LENGTH and <= MAX_PASSWORD_LENGTH
                && password.All(character => character is >= '!' and <= '~')
                && password.Any(character => character is >= 'A' and <= 'Z')
                && password.Any(character => character is >= '0' and <= '9')
                && password.Any(character => !char.IsLetterOrDigit(character));
        }
    }
}
