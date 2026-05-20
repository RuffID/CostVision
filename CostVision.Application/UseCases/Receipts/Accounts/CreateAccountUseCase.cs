using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.Receipts.Accounts.Helpers;
using CostVision.Domain.Models.Enums.Authorization;
using CostVision.Domain.Models.Receipts;

namespace CostVision.Application.UseCases.Receipts.Accounts
{
    public class CreateAccountUseCase(IUnitOfWork unitOfWork) : ICreateAccountUseCase
    {
        public async Task<ServiceResult<Account>> ExecuteAsync(Guid ownerUserId, CreateAccountRequest request, CancellationToken ct)
        {
            ServiceResult<string> colorHexResult = AccountColorHexNormalizer.Normalize(request.ColorHex);
            if (!colorHexResult.Success || colorHexResult.Data == null)
                return ServiceResult<Account>.Fail(colorHexResult.Error!.StatusCode, colorHexResult.Error.Message);

            Account account = new()
            {
                Name = request.Name,
                Description = request.Description,
                ColorHex = colorHexResult.Data,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = ownerUserId
            };

            AccountMember ownerMember = new()
            {
                UserId = ownerUserId,
                Account = account,
                Role = AccountAccessRole.Owner
            };

            await unitOfWork.ExecuteInTransaction(async () =>
            {
                unitOfWork.Account.Create(account);
                unitOfWork.AccountMember.Create(ownerMember);
            }, ct);

            return ServiceResult<Account>.Ok(account);
        }
    }
}
