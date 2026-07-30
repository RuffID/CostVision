using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Domain.Models.Enums.Authorization;
using CostVision.Domain.Models.Receipts;

namespace CostVision.Application.UseCases.Receipts.Accounts
{
    public class CreateAccountUseCase(IUnitOfWork unitOfWork) : ICreateAccountUseCase
    {
        public async Task<ServiceResult<Account>> ExecuteAsync(Guid ownerUserId, CreateAccountRequest request, CancellationToken ct)
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
                return ServiceResult<Account>.Fail(400, error ?? "Не удалось создать счёт.");

            AccountMember ownerMember = account.Members.Single(member => member.Role == AccountAccessRole.Owner);

            await unitOfWork.ExecuteInTransaction(async () =>
            {
                unitOfWork.Account.Create(account);
                unitOfWork.AccountMember.Create(ownerMember);
            }, ct);

            return ServiceResult<Account>.Ok(account);
        }
    }
}
