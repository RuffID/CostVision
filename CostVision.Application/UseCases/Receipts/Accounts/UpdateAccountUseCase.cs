using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.Receipts.Accounts.Helpers;
using CostVision.Domain.Models.Receipts;

namespace CostVision.Application.UseCases.Receipts.Accounts
{
    public class UpdateAccountUseCase(IUnitOfWork unitOfWork) : IUpdateAccountUseCase
    {
        public async Task<ServiceResult<Account>> ExecuteAsync(Guid ownerUserId, Account account, CancellationToken ct)
        {
            ServiceResult<string> colorHexResult = AccountColorHexNormalizer.Normalize(account.ColorHex);
            if (!colorHexResult.Success || colorHexResult.Data == null)
                return ServiceResult<Account>.Fail(colorHexResult.Error!.StatusCode, colorHexResult.Error.Message);

            Account? current = await unitOfWork.Account.GetItemByPredicateAsync(item => item.Id == account.Id && item.CreatedByUserId == ownerUserId, ct: ct);
            if (current == null)
                return ServiceResult<Account>.Fail(404, "Счёт не найден.");

            current.Name = account.Name;
            current.Description = account.Description;
            current.ColorHex = colorHexResult.Data;
            current.IsArchived = account.IsArchived;

            await unitOfWork.SaveChangesAsync(ct);

            account.ColorHex = current.ColorHex;
            account.IsArchived = current.IsArchived;

            return ServiceResult<Account>.Ok(account);
        }
    }
}
