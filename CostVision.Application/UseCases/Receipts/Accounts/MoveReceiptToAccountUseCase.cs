using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.Receipts.Accounts.Helpers;
using CostVision.Domain.Models.Receipts;
using Microsoft.EntityFrameworkCore;

namespace CostVision.Application.UseCases.Receipts.Accounts
{
    public class MoveReceiptToAccountUseCase(IUnitOfWork unitOfWork) : IMoveReceiptToAccountUseCase
    {
        public async Task<ServiceResult<bool>> ExecuteAsync(Guid sourceAccountId, Guid targetAccountId, Guid receiptId, Guid currentUserId, CancellationToken ct)
        {
            if (receiptId == Guid.Empty)
                return ServiceResult<bool>.Fail(400, "Некорректный идентификатор чека.");

            if (sourceAccountId == Guid.Empty || targetAccountId == Guid.Empty)
                return ServiceResult<bool>.Fail(400, "Некорректный идентификатор счёта.");

            if (sourceAccountId == targetAccountId)
                return ServiceResult<bool>.Fail(400, "Счёт назначения должен отличаться от исходного счёта.");

            Receipt? receipt = await unitOfWork.Receipt.GetItemByPredicateAsync(
                receipt => receipt.Id == receiptId,
                asNoTracking: false,
                include: query => query
                    .Include(receipt => receipt.Accounts)
                        .ThenInclude(link => link.Account)
                            .ThenInclude(account => account!.Members)
                    .AsSplitQuery(),
                ct: ct);
            if (receipt == null)
                return ServiceResult<bool>.Fail(404, "Чек не найден.");

            if (receipt.CreatedByUserId != currentUserId)
                return ServiceResult<bool>.Fail(403, "Можно изменять только собственный чек.");

            ReceiptAccount? sourceLink = receipt.Accounts.FirstOrDefault(link => link.AccountId == sourceAccountId);
            if (sourceLink == null)
                return ServiceResult<bool>.Fail(404, "Чек не привязан к выбранному исходному счёту.");

            if (receipt.Accounts.Any(link => link.AccountId == targetAccountId))
                return ServiceResult<bool>.Fail(409, "Чек уже привязан к счёту назначения.");

            Account? sourceAccount = sourceLink.Account;
            if (sourceAccount == null)
                return ServiceResult<bool>.Fail(404, "Исходный счёт не найден.");

            ServiceResult<bool> sourceAccessResult = AccountReceiptAccessValidator.ValidateModificationAccess(sourceAccount, currentUserId);
            if (!sourceAccessResult.Success)
                return ServiceResult<bool>.Fail(sourceAccessResult.Error!.StatusCode, sourceAccessResult.Error.Message);

            Account? targetAccount = await unitOfWork.Account.GetByIdWithMembersAsync(targetAccountId, asNoTracking: false, ct: ct);
            if (targetAccount == null)
                return ServiceResult<bool>.Fail(404, "Счёт назначения не найден.");

            ServiceResult<bool> targetAccessResult = AccountReceiptAccessValidator.ValidateModificationAccess(targetAccount, currentUserId);
            if (!targetAccessResult.Success)
                return ServiceResult<bool>.Fail(targetAccessResult.Error!.StatusCode, targetAccessResult.Error.Message);

            Receipt? duplicateReceiptInTargetAccount = await unitOfWork.Receipt.GetItemByPredicateAsync(item =>
                item.Id != receiptId &&
                item.FiscalDriveNumber == receipt.FiscalDriveNumber &&
                item.FiscalDocumentNumber == receipt.FiscalDocumentNumber &&
                item.FiscalSign == receipt.FiscalSign &&
                item.DateTime == receipt.DateTime &&
                item.TotalSum == receipt.TotalSum &&
                item.OperationType == receipt.OperationType &&
                item.Accounts.Any(link => link.AccountId == targetAccountId),
                asNoTracking: true,
                ct: ct);

            if (duplicateReceiptInTargetAccount != null)
                return ServiceResult<bool>.Fail(409, "В счёте назначения уже есть такой же чек.");

            unitOfWork.ReceiptAccount.Delete(sourceLink);
            unitOfWork.ReceiptAccount.Create(new ReceiptAccount
            {
                ReceiptId = receiptId,
                AccountId = targetAccountId
            });

            await unitOfWork.SaveChangesAsync(ct);

            return ServiceResult<bool>.Ok(true);
        }
    }
}
