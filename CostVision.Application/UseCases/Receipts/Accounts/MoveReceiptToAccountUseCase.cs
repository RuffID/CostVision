using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.Receipts.Accounts.Helpers;
using CostVision.Domain.Models.Receipts;
using Microsoft.EntityFrameworkCore;

namespace CostVision.Application.UseCases.Receipts.Accounts
{
    public class MoveReceiptToAccountUseCase(IUnitOfWork unitOfWork) : IMoveReceiptToAccountUseCase
    {
        public async Task<ServiceResult> ExecuteAsync(Guid sourceAccountId, Guid targetAccountId, Guid receiptId, Guid currentUserId, CancellationToken ct)
        {
            if (receiptId == Guid.Empty)
                return ServiceResult.Fail(ServiceErrorType.Validation, "Некорректный идентификатор чека.");

            if (targetAccountId == Guid.Empty)
                return ServiceResult.Fail(ServiceErrorType.Validation, "Некорректный идентификатор счёта.");

            if (sourceAccountId != Guid.Empty && sourceAccountId == targetAccountId)
                return ServiceResult.Fail(ServiceErrorType.Validation, "Счёт назначения должен отличаться от исходного счёта.");

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
                return ServiceResult.Fail(ServiceErrorType.NotFound, "Чек не найден.");

            if (receipt.CreatedByUserId != currentUserId)
                return ServiceResult.Fail(ServiceErrorType.Forbidden, "Можно изменять только собственный чек.");

            ReceiptAccount? sourceLink = sourceAccountId == Guid.Empty
                ? null
                : receipt.Accounts.FirstOrDefault(link => link.AccountId == sourceAccountId);
            if (sourceAccountId != Guid.Empty && sourceLink == null)
                return ServiceResult.Fail(ServiceErrorType.NotFound, "Чек не привязан к выбранному исходному счёту.");

            if (receipt.Accounts.Any(link => link.AccountId == targetAccountId))
                return ServiceResult.Fail(ServiceErrorType.Conflict, "Чек уже привязан к счёту назначения.");

            Account? sourceAccount = sourceLink?.Account;
            if (sourceAccountId != Guid.Empty && sourceAccount == null)
                return ServiceResult.Fail(ServiceErrorType.NotFound, "Исходный счёт не найден.");

            if (sourceAccount != null)
            {
                ServiceResult sourceAccessResult = AccountReceiptAccessValidator.ValidateModificationAccess(sourceAccount, currentUserId);
                if (!sourceAccessResult.Success)
                    return sourceAccessResult.PropagateFailure();
            }

            Account? targetAccount = await unitOfWork.Account.GetItemByPredicateAsync(
                account => account.Id == targetAccountId,
                asNoTracking: false,
                include: query => query.Include(account => account.Members),
                ct: ct);
            if (targetAccount == null)
                return ServiceResult.Fail(ServiceErrorType.NotFound, "Счёт назначения не найден.");

            ServiceResult targetAccessResult = AccountReceiptAccessValidator.ValidateModificationAccess(targetAccount, currentUserId);
            if (!targetAccessResult.Success)
                return targetAccessResult.PropagateFailure();

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
                return ServiceResult.Fail(ServiceErrorType.Conflict, "В счёте назначения уже есть такой же чек.");

            if (!receipt.TryMoveAccount(
                    sourceAccountId,
                    targetAccountId,
                    out ReceiptAccount? removedLink,
                    out ReceiptAccount? targetLink,
                    out string? moveError))
                return ServiceResult.Fail(
                    ServiceErrorType.Conflict,
                    moveError ?? throw new InvalidOperationException("Доменная операция не вернула причину отказа."));

            if (sourceLink != null)
                unitOfWork.ReceiptAccount.Delete(removedLink!);

            unitOfWork.ReceiptAccount.Create(targetLink!);

            await unitOfWork.SaveChangesAsync(ct);

            return ServiceResult.Ok();
        }
    }
}
