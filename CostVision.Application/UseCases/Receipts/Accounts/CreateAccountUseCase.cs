using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Domain.Models.Enums.Authorization;
using CostVision.Domain.Models.Receipts;

namespace CostVision.Application.UseCases.Receipts.Accounts
{
    public class CreateAccountUseCase(IUnitOfWork unitOfWork) : ICreateAccountUseCase
    {
        public async Task<ServiceResult<UserAccountViewModel>> ExecuteAsync(Guid ownerUserId, CreateAccountRequest request, CancellationToken ct)
        {
            bool isCreated = Account.TryCreate(
                request.Name,
                request.Description,
                request.ColorHex,
                ownerUserId,
                DateTime.UtcNow,
                out Account? account,
                out string? error);

            if (!isCreated || account == null)
                return ServiceResult<UserAccountViewModel>.Fail(ServiceErrorType.Validation, error ?? "Не удалось создать счёт.");

            AccountMember ownerMember = account.Members.Single(member => member.Role == AccountAccessRole.Owner);

            await unitOfWork.ExecuteInTransaction(_ =>
            {
                unitOfWork.Account.Create(account);
                unitOfWork.AccountMember.Create(ownerMember);
                return Task.CompletedTask;
            }, ct);

            return ServiceResult<UserAccountViewModel>.Ok(new UserAccountViewModel
            {
                Id = account.Id,
                Name = account.Name,
                Description = account.Description,
                ColorHex = account.ColorHex,
                IsActive = !account.IsArchived,
                CanManage = true,
                AccessRole = AccountAccessRole.Owner
            });
        }
    }
}
