using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Models.Requests.MoneyMovements;
using CostVision.Application.Models.Responses.Results;
using CostVision.Domain.Models.MoneyMovements;
using CostVision.Domain.Models.Enums.MoneyMovements;
using CostVision.Domain.Models.Receipts;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Linq.Expressions;

namespace CostVision.Application.UseCases.MoneyMovements
{
    public class MoveMoneyMovementToAccountUseCase(IMoneyMovementEditUnitOfWork unitOfWork) : IMoveMoneyMovementToAccountUseCase
    {
        public async Task<ServiceResult> ExecuteAsync(MoveMoneyMovementToAccountRequest request, Guid currentUserId, CancellationToken ct)
        {
            if (request.MoneyMovementId == Guid.Empty || request.TargetAccountId == Guid.Empty)
            {
                return ServiceResult.Fail(ServiceErrorType.Validation, "Некорректный идентификатор операции или счёта.");
            }

            if (request.SourceAccountId != Guid.Empty && request.SourceAccountId == request.TargetAccountId)
            {
                return ServiceResult.Fail(ServiceErrorType.Validation, "Исходный и целевой счёт совпадают.");
            }

            if (request.SourceAccountId != Guid.Empty)
            {
                ServiceResult<AccountMember> sourceAccess = await MoneyMovementAccountAccessValidator.GetEditableAccountMemberAsync(
                    unitOfWork.AccountMember, request.SourceAccountId, currentUserId, ct);
                if (!sourceAccess.Success)
                {
                    return sourceAccess.PropagateFailure();
                }
            }

            ServiceResult<AccountMember> targetAccess = await MoneyMovementAccountAccessValidator.GetEditableAccountMemberAsync(
                unitOfWork.AccountMember, request.TargetAccountId, currentUserId, ct);
            if (!targetAccess.Success)
            {
                return targetAccess.PropagateFailure();
            }

            ServiceResult? result = null;
            MoneyMovement? moved = null;
            try
            {
                await unitOfWork.ExecuteInTransaction(async transactionCt =>
                {
                    Expression<Func<MoneyMovement, bool>> movementPredicate;
                    if (request.SourceAccountId != Guid.Empty)
                    {
                        movementPredicate = item => item.Id == request.MoneyMovementId && item.AccountId == request.SourceAccountId;
                    }
                    else
                    {
                        movementPredicate = item => item.Id == request.MoneyMovementId &&
                            item.AccountId == Guid.Empty && item.CreatedByUserId == currentUserId;
                    }

                    MoneyMovement? movement = await unitOfWork.MoneyMovement.GetItemByPredicateAsync(
                        movementPredicate,
                        include: query => query.Include(item => item.ReceiptLinks).ThenInclude(link => link.Receipt)
                            .ThenInclude(receipt => receipt!.Accounts).AsSplitQuery(), ct: transactionCt);
                    if (movement == null)
                    {
                        result = ServiceResult.Fail(ServiceErrorType.NotFound, "Операция не найдена в исходном счёте.");
                        return;
                    }

                    foreach (MoneyMovementReceipt link in movement.ReceiptLinks)
                    {
                        Receipt receipt = link.Receipt
                            ?? throw new InvalidOperationException("Не загружен чек для проверки переноса.");
                        bool sameAccount = receipt.Accounts.Any(account => account.AccountId == request.TargetAccountId);
                        bool ownUnplacedReceipt = receipt.Accounts.Count == 0 && receipt.CreatedByUserId == currentUserId;
                        if (!sameAccount && !ownUnplacedReceipt)
                        {
                            result = ServiceResult.Fail(ServiceErrorType.Conflict,
                                "Перенос нарушит связь с чеком. Сначала отвяжите несовместимые связи вручную.");
                            return;
                        }
                    }

                    if (movement.Source == MoneyMovementSource.BankStatementImport)
                    {
                        MoneyMovement? duplicate = await unitOfWork.MoneyMovement.GetItemByPredicateAsync(
                            BankStatementRowKey.From(movement).InAccount(request.TargetAccountId),
                            asNoTracking: true, ct: transactionCt);
                        if (duplicate != null)
                        {
                            result = ServiceResult.Fail(ServiceErrorType.Conflict,
                                "В счёте назначения уже есть такая строка выписки.");
                            return;
                        }
                    }

                    if (!movement.TryMoveToAccount(request.TargetAccountId, out string? error))
                    {
                        result = ServiceResult.Fail(ServiceErrorType.Validation, error!);
                        return;
                    }

                    moved = movement;
                    await unitOfWork.SaveChangesAsync(transactionCt);
                    result = ServiceResult.Ok();
                }, IsolationLevel.Serializable, ct);
            }
            catch (ReceiptWriteConflictException)
            {
                RestoreAccount(moved, request.SourceAccountId);
                return ServiceResult.Fail(ServiceErrorType.Conflict, "Конкурентный перенос отменён. Повторите запрос.");
            }
            catch
            {
                RestoreAccount(moved, request.SourceAccountId);
                throw;
            }

            return result ?? throw new InvalidOperationException("Транзакция переноса операции завершилась без результата.");
        }

        private static void RestoreAccount(MoneyMovement? movement, Guid sourceAccountId)
        {
            if (movement == null || sourceAccountId == Guid.Empty)
            {
                return;
            }

            if (!movement.TryMoveToAccount(sourceAccountId, out string? error))
            {
                throw new InvalidOperationException(error);
            }
        }
    }
}
