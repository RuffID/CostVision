using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Domain.Models.Enums.Authorization;
using CostVision.Domain.Models.Receipts;

namespace CostVision.Application.UseCases.Receipts.Accounts
{
    public class UpdateAccountUseCase(IUnitOfWork unitOfWork) : IUpdateAccountUseCase
    {
        public async Task<ServiceResult<UserAccountViewModel>> ExecuteAsync(Guid ownerUserId, UpdateAccountRequest request, CancellationToken ct)
        {
            if (request.AccountId == Guid.Empty)
                return ServiceResult<UserAccountViewModel>.Fail(ServiceErrorType.Validation, "Некорректный идентификатор счёта.");

            Account? current = await unitOfWork.Account.GetItemByPredicateAsync(item => item.Id == request.AccountId && item.CreatedByUserId == ownerUserId, ct: ct);
            if (current == null)
                return ServiceResult<UserAccountViewModel>.Fail(ServiceErrorType.NotFound, "Счёт не найден.");

            if (!current.TryUpdateDetails(request.Name, request.Description, request.ColorHex, out string? error))
                return ServiceResult<UserAccountViewModel>.Fail(ServiceErrorType.Validation, error ?? "Некорректные данные счёта.");

            if (request.IsActive)
                current.Restore();
            else
                current.Archive();

            await unitOfWork.SaveChangesAsync(ct);

            return ServiceResult<UserAccountViewModel>.Ok(new UserAccountViewModel
            {
                Id = current.Id,
                Name = current.Name,
                Description = current.Description,
                ColorHex = current.ColorHex,
                IsActive = !current.IsArchived,
                CanManage = true,
                AccessRole = AccountAccessRole.Owner
            });
        }
    }
}
