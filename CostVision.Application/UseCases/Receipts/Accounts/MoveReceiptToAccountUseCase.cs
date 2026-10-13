using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.Receipts.Accounts.Helpers;
using CostVision.Domain.Models.Receipts;
using CostVision.Domain.Models.MoneyMovements;
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

            ServiceResult? result = null;
            Receipt? receipt = null;
            Account? sourceAccount = null;
            bool moved = false;
            try
            {
                await unitOfWork.ExecuteInTransaction(async transactionCt =>
                {
                    receipt = await unitOfWork.Receipt.GetItemByPredicateAsync(
                        receipt => receipt.Id == receiptId,
                        asNoTracking: false,
                        include: query => query
                            .Include(receipt => receipt.Accounts)
                                .ThenInclude(link => link.Account)
                                    .ThenInclude(account => account!.Members)
                            .Include(receipt => receipt.MoneyMovementLinks)
                                .ThenInclude(link => link.MoneyMovement)
                            .AsSplitQuery(),
                        ct: transactionCt);

                    if (receipt == null)
                    {
                        result = ServiceResult.Fail(ServiceErrorType.NotFound, "Чек не найден.");
                        return;
                    }

                    if (receipt.CreatedByUserId != currentUserId)
                    {
                        result = ServiceResult.Fail(ServiceErrorType.Forbidden, "Можно изменять только собственный чек.");
                        return;
                    }

                    ReceiptAccount? sourceLink = null;
                    if (sourceAccountId != Guid.Empty)
                    {
                        sourceLink = receipt.Accounts.FirstOrDefault(link => link.AccountId == sourceAccountId);
                    }

                    if (sourceAccountId != Guid.Empty && sourceLink == null)
                    {
                        result = ServiceResult.Fail(ServiceErrorType.NotFound, "Чек не привязан к выбранному исходному счёту.");
                        return;
                    }

                    if (receipt.Accounts.Any(link => link.AccountId == targetAccountId))
                    {
                        result = ServiceResult.Fail(ServiceErrorType.Conflict, "Чек уже привязан к счёту назначения.");
                        return;
                    }

                    sourceAccount = sourceLink?.Account;
                    if (sourceAccountId != Guid.Empty && sourceAccount == null)
                    {
                        result = ServiceResult.Fail(ServiceErrorType.NotFound, "Исходный счёт не найден.");
                        return;
                    }

                    if (sourceAccount != null)
                    {
                        ServiceResult sourceAccessResult = AccountReceiptAccessValidator.ValidateModificationAccess(sourceAccount, currentUserId);
                        if (!sourceAccessResult.Success)
                        {
                            result = sourceAccessResult.PropagateFailure();
                            return;
                        }
                    }

                    Account? targetAccount = await unitOfWork.Account.GetItemByPredicateAsync(
                        account => account.Id == targetAccountId,
                        asNoTracking: false,
                        include: query => query.Include(account => account.Members),
                        ct: transactionCt);

                    if (targetAccount == null)
                    {
                        result = ServiceResult.Fail(ServiceErrorType.NotFound, "Счёт назначения не найден.");
                        return;
                    }

                    ServiceResult targetAccessResult = AccountReceiptAccessValidator.ValidateModificationAccess(targetAccount, currentUserId);
                    if (!targetAccessResult.Success)
                    {
                        result = targetAccessResult.PropagateFailure();
                        return;
                    }

                    foreach (MoneyMovementReceipt link in receipt.MoneyMovementLinks)
                    {
                        MoneyMovement movement = link.MoneyMovement
                            ?? throw new InvalidOperationException("Не загружена операция для проверки переноса чека.");
                        bool compatible = movement.AccountId == targetAccountId ||
                            receipt.Accounts.Any(account => account.AccountId != sourceAccountId &&
                                account.AccountId == movement.AccountId);
                        if (!compatible)
                        {
                            result = ServiceResult.Fail(ServiceErrorType.Conflict,
                                "Перенос нарушит денежную связь. Сначала отвяжите несовместимые связи вручную.");
                            return;
                        }
                    }

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
                if (receipt != null)
                {
                    RestoreAccounts(receipt, sourceAccount, targetAccountId, moved);
                }

                return ServiceResult.Fail(ServiceErrorType.Conflict, conflict.Message);
            }
            catch
            {
                if (receipt != null)
                {
                    RestoreAccounts(receipt, sourceAccount, targetAccountId, moved);
                }

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
