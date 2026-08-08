using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.Service.Authorize;
using CostVision.Domain.Models.Authorization;
using CostVision.Application.Models.Requests.Authorize;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.Authorize.Users.Helpers;
using Microsoft.EntityFrameworkCore;

namespace CostVision.Application.UseCases.Authorize.Users
{
    public class UpdateUserUseCase(IUnitOfWork unitOfWork, IPasswordHasher passwordHasher) : IUpdateUserUseCase
    {
        public async Task<ServiceResult> ExecuteAsync(UserUpsertRequest request, CancellationToken ct)
        {
            if (!request.Id.HasValue)
                return ServiceResult.Fail(ServiceErrorType.Validation, "Некорректный id пользователя.");

            ServiceResult<List<Guid>> validationResult = UserUpsertRequestValidator.ValidateUpsertRequest(request, requirePassword: false);
            if (!validationResult.Success)
                return validationResult.PropagateFailure();

            User? user = await unitOfWork.User.GetItemByPredicateAsync(
                user => user.Id == request.Id.Value,
                asNoTracking: false,
                include: query => query.Include(user => user.UserRoles),
                ct: ct);
            if (user == null)
                return ServiceResult.Fail(ServiceErrorType.NotFound, "Пользователь не найден.");

            string normalizedLogin = request.Login.Trim().ToUpperInvariant();
            if (!string.Equals(user.Login, request.Login.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                User? conflictUser = await unitOfWork.User.GetItemByPredicateAsync(
                    user => user.Login.ToUpper() == normalizedLogin,
                    asNoTracking: true,
                    ct: ct);
                if (conflictUser != null && conflictUser.Id != user.Id)
                    return ServiceResult.Fail(ServiceErrorType.Conflict, "Пользователь с таким логином уже существует.");
            }

            string? passwordHash = string.IsNullOrWhiteSpace(request.Password)
                ? null
                : passwordHasher.Hash(request.Password.Trim());

            if (!user.TryUpdate(
                    request.Login,
                    request.Name,
                    passwordHash,
                    validationResult.Data,
                    out string? error))
                return ServiceResult.Fail(ServiceErrorType.Validation, error!);

            await unitOfWork.SaveChangesAsync(ct);

            return ServiceResult.Ok();
        }
    }
}
