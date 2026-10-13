using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.Receipts.Accounts.Helpers;
using CostVision.Domain.Models.Receipts;
using Microsoft.EntityFrameworkCore;
using CostVision.Application.Extensions;
using System.Data;

namespace CostVision.Application.UseCases.Receipts.Accounts
{
    public class MoveReceiptToAccountUseCase(IUnitOfWork unitOfWork) : IMoveReceiptToAccountUseCase
    {
        public async Task<ServiceResult> ExecuteAsync(Guid sourceAccountId, Guid targetAccountId, Guid receiptId, Guid currentUserId, CancellationToken ct)
        {
            if (receiptId == Guid.Empty)
            {
                return ServiceResult.Fail(ServiceErrorType.Validation, "Некорректный идентификатор чека.");
            }

            if (targetAccountId == Guid.Empty)
            {
                return ServiceResult.Fail(ServiceErrorType.Validation, "Некорректный идентификатор счёта.");
            }

            if (sourceAccountId != Guid.Empty && sourceAccountId == targetAccountId)
            {
                return ServiceResult.Fail(ServiceErrorType.Validation, "Счёт назначения должен отличаться от исходного счёта.");
            }

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
            {
                return ServiceResult.Fail(ServiceErrorType.NotFound, "Чек не найден.");
            }

            if (receipt.CreatedByUserId != currentUserId)
            {
                return ServiceResult.Fail(ServiceErrorType.Forbidden, "Можно изменять только собственный чек.");
            }

            ReceiptAccount? sourceLink = null;
            if (sourceAccountId != Guid.Empty)
            {
                sourceLink = receipt.Accounts.FirstOrDefault(link => link.AccountId == sourceAccountId);
            }

            if (sourceAccountId != Guid.Empty && sourceLink == null)
            {
                return ServiceResult.Fail(ServiceErrorType.NotFound, "Чек не привязан к выбранному исходному счёту.");
            }

            if (receipt.Accounts.Any(link => link.AccountId == targetAccountId))
            {
                return ServiceResult.Fail(ServiceErrorType.Conflict, "Чек уже привязан к счёту назначения.");
            }

            Account? sourceAccount = sourceLink?.Account;
            if (sourceAccountId != Guid.Empty && sourceAccount == null)
            {
                return ServiceResult.Fail(ServiceErrorType.NotFound, "Исходный счёт не найден.");
            }

            if (sourceAccount != null)
            {
                ServiceResult sourceAccessResult = AccountReceiptAccessValidator.ValidateModificationAccess(sourceAccount, currentUserId);
                if (!sourceAccessResult.Success)
                {
                    return sourceAccessResult.PropagateFailure();
                }
            }

            Account? targetAccount = await unitOfWork.Account.GetItemByPredicateAsync(
                account => account.Id == targetAccountId,
                asNoTracking: false,
                include: query => query.Include(account => account.Members),
                ct: ct);

            if (targetAccount == null)
            {
                return ServiceResult.Fail(ServiceErrorType.NotFound, "Счёт назначения не найден.");
            }

            ServiceResult targetAccessResult = AccountReceiptAccessValidator.ValidateModificationAccess(targetAccount, currentUserId);
            if (!targetAccessResult.Success)
            {
                return targetAccessResult.PropagateFailure();
            }

            ServiceResult? result = null;
            bool moved = false;
            try
            {
                await unitOfWork.ExecuteInTransaction(async transactionCt =>
                {
                    Receipt? duplicate = await unitOfWork.Receipt.GetItemByPredicateAsync(
                        receipt.GetIdentity().InAccount(targetAccountId, receiptId),
                        asNoTracking: true, ct: transactionCt);
                    if (duplicate != null)
                    {
                        result = ServiceResult.Fail(ServiceErrorType.Conflict, "В счёте назначения уже есть такой же чек.");
                        return;
                    }

                    if (!receipt.TryMoveAccount(sourceAccountId, targetAccountId,
                            out ReceiptAccount? removedLink, out ReceiptAccount? targetLink, out string? moveError))
                    {
                        result = ServiceResult.Fail(ServiceErrorType.Conflict,
                            moveError ?? throw new InvalidOperationException("Доменная операция не вернула причину отказа."));
                        return;
                    }

                    moved = true;
                    if (removedLink != null)
                    {
                        unitOfWork.ReceiptAccount.Delete(removedLink);
                    }

                    unitOfWork.ReceiptAccount.Create(targetLink!);
                    await unitOfWork.SaveChangesAsync(transactionCt);
                    result = ServiceResult.Ok();
                }, IsolationLevel.Serializable, ct);
            }
            catch (ReceiptWriteConflictException conflict)
            {
                RestoreAccounts(receipt, sourceAccount, targetAccountId, moved);
                return ServiceResult.Fail(ServiceErrorType.Conflict, conflict.Message);
            }
            catch
            {
                RestoreAccounts(receipt, sourceAccount, targetAccountId, moved);
                throw;
            }

            return result ?? throw new InvalidOperationException("Транзакция переноса чека завершилась без результата.");
        }

        private static void RestoreAccounts(Receipt receipt, Account? sourceAccount, Guid targetAccountId, bool moved)
        {
            if (!moved)
            {
                return;
            }

            if (!receipt.TryRemoveAccount(targetAccountId, out _, out string? error))
            {
                throw new InvalidOperationException(error);
            }

            if (sourceAccount != null && !receipt.TryAddAccount(sourceAccount, out _, out error))
            {
                throw new InvalidOperationException(error);
            }
        }
    }
}
