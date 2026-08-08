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
            if (!validationResult.Success)
                return validationResult.PropagateFailure();

            string normalizedLogin = request.Login.Trim().ToUpperInvariant();
            User? existingUser = await unitOfWork.User.GetItemByPredicateAsync(
                user => user.Login.ToUpper() == normalizedLogin,
                asNoTracking: true,
                ct: ct);
            if (existingUser != null)
                return ServiceResult.Fail(ServiceErrorType.Conflict, "Пользователь с таким логином уже существует");

            if (!User.TryCreate(
                    request.Login,
                    request.Name,
                    passwordHasher.Hash(request.Password!.Trim()),
                    validationResult.Data,
                    DateTime.UtcNow,
                    out User? user,
                    out string? error))
                return ServiceResult.Fail(ServiceErrorType.Validation, error!);

            unitOfWork.User.Create(user!);
            await unitOfWork.SaveChangesAsync(ct);

            return ServiceResult.Ok();
        }
    }
}
