using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Models.Dtos.MoneyMovements;
using CostVision.Application.Models.Requests.MoneyMovements;
using CostVision.Application.Models.Responses.Results;
using CostVision.Domain.Models.Enums.MoneyMovements;
using CostVision.Domain.Models.MoneyMovements;
using System.Data;

using CostVision.Application.Extensions;

namespace CostVision.Application.UseCases.MoneyMovements
{
    public class ImportMoneyMovementsUseCase(IBankStatementImportUnitOfWork unitOfWork) : IImportMoneyMovementsUseCase
    {
        public async Task<ServiceResult<BankStatementImportResultDto>> ExecuteAsync(SaveBankStatementImportRequest request, Guid currentUserId, CancellationToken ct)
        {
            ServiceResult shape = RequestShapeValidation.Validate(request);
            if (!shape.Success)
            {
                return shape.PropagateFailure<BankStatementImportResultDto>();
            }

            if (request.AccountId == Guid.Empty)
            {
                return ServiceResult<BankStatementImportResultDto>.Fail(ServiceErrorType.Validation, "Выберите счёт для импорта.");
            }

            if (request.Rows.Count == 0)
            {
                return ServiceResult<BankStatementImportResultDto>.Fail(ServiceErrorType.Validation, "Нет строк для импорта.");
            }

            ServiceResult<CostVision.Domain.Models.Receipts.AccountMember> accountAccess = await MoneyMovementAccountAccessValidator.GetEditableAccountMemberAsync(unitOfWork.AccountMember, request.AccountId, currentUserId, ct, requireActive: true);
            if (!accountAccess.Success)
            {
                return accountAccess.PropagateFailure<BankStatementImportResultDto>();
            }

            BankStatementImportResultDto result = new();
            List<ValidImportRow> validRows =
                ValidateRows(request.AccountId, request.Rows, currentUserId, result);
            if (validRows.Count == 0)
            {
                return ServiceResult<BankStatementImportResultDto>.Ok(result);
            }

            try
            {
                await unitOfWork.ExecuteInTransaction(async transactionCt =>
                {
                    Dictionary<BankStatementRowKey, MoneyMovement> accepted = new();
                    foreach (ValidImportRow validRow in validRows)
                    {
                        BankStatementImportRowRequest row = validRow.Row;
                        MoneyMovement movement = validRow.Movement;
                        BankStatementRowKey key = BankStatementRowKey.From(movement);
                        accepted.TryGetValue(key, out MoneyMovement? duplicate);
                        if (duplicate == null)
                        {
                            List<MoneyMovement> duplicates = await unitOfWork.MoneyMovement.GetItemsByPredicateAsync(
                                key.InAccount(request.AccountId), asNoTracking: false, ct: transactionCt);
                            if (duplicates.Count > 1)
                            {
                                AddError(result, validRow.Index, "Найдено несколько операций с ключом строки. Требуется ручная сверка.", row.ImportComment);
                                continue;
                            }

                            duplicate = duplicates.SingleOrDefault();
                        }

                        if (row.DuplicateMoneyMovementId.HasValue &&
                            (duplicate == null || duplicate.Id != row.DuplicateMoneyMovementId.Value))
                        {
                            AddError(result, validRow.Index, "Выбранная операция больше не совпадает со строкой выписки. Повторите предпросмотр.", row.ImportComment);
                            continue;
                        }

                        if (duplicate != null)
                        {
                            // SQL-колляция может сравнивать строки иначе, чем типизированный ключ.
                            if (BankStatementRowKey.From(duplicate) != key || duplicate.AccountId != request.AccountId ||
                                duplicate.Source != MoneyMovementSource.BankStatementImport)
                            {
                                AddError(result, validRow.Index, "Ключ существующей операции не совпадает со строкой выписки.", row.ImportComment);
                                continue;
                            }

                            if (!row.ReplaceDuplicate)
                            {
                                AddError(result, validRow.Index, "В импорте есть повторяющаяся операция. Строка пропущена.", row.ImportComment, duplicate);
                                continue;
                            }

                            if (!duplicate.TryReplaceFromBankStatement(movement.Amount, movement.Type, movement.OccurredAt,
                                    movement.Comment, movement.ImportComment, DateTime.UtcNow, out string? error))
                            {
                                AddError(result, validRow.Index, error!, row.ImportComment);
                                continue;
                            }

                            accepted[key] = duplicate;
                            result.UpdatedCount++;
                            continue;
                        }

                        unitOfWork.MoneyMovement.Create(movement);
                        accepted.Add(key, movement);
                        result.CreatedCount++;
                    }

                    if (result.CreatedCount + result.UpdatedCount > 0)
                    {
                        await unitOfWork.SaveChangesAsync(transactionCt);
                    }
                }, IsolationLevel.Serializable, ct);
            }
            catch (ReceiptWriteConflictException)
            {
                return ServiceResult<BankStatementImportResultDto>.Fail(ServiceErrorType.Conflict,
                    "Конкурентный импорт отменён. Обновите данные и повторите запрос.");
            }

            return ServiceResult<BankStatementImportResultDto>.Ok(result);
        }

        private static List<ValidImportRow> ValidateRows(
            Guid accountId,
            List<BankStatementImportRowRequest> rows,
            Guid currentUserId,
            BankStatementImportResultDto result)
        {
            List<ValidImportRow> validRows = new();

            foreach ((BankStatementImportRowRequest row, int index) in rows.Select((row, index) => (row, index)))
            {
                if (row.Amount != decimal.Round(row.Amount, 2) || row.Amount >= 10000000000000000m)
                {
                    AddError(result, index, "Сумма строки должна точно помещаться в денежное поле 18,2 без округления.", row.ImportComment);
                    continue;
                }

                if (!MoneyMovement.TryCreateBankStatementImport(
                        accountId,
                        row.Amount,
                        row.Type,
                        row.OccurredAt,
                        row.Comment,
                        row.ImportComment,
                        currentUserId,
                        DateTime.UtcNow,
                        out MoneyMovement? movement,
                        out string? error))
                {
                    AddError(result, index, error!, row.ImportComment);
                    continue;
                }

                validRows.Add(new ValidImportRow(index, row, movement!));
            }

            return validRows;
        }

        private static void AddError(BankStatementImportResultDto result, int rowIndex, string message, string? rawText, MoneyMovement? duplicate = null)
        {
            BankStatementImportLineErrorDto error = new()
            {
                LineNumber = rowIndex + 1,
                Message = message,
                RawText = rawText ?? string.Empty,
                CanReplaceDuplicate = duplicate != null
            };

            if (duplicate != null && duplicate.Id != Guid.Empty)
            {
                error.DuplicateMoneyMovementId = duplicate.Id;
            }

            result.Errors.Add(error);

            result.ErrorCount = result.Errors.Count;
        }

        private readonly record struct ValidImportRow(int Index, BankStatementImportRowRequest Row, MoneyMovement Movement);
    }
}
