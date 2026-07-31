using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Models.Dtos.MoneyMovements;
using CostVision.Application.Models.Requests.MoneyMovements;
using CostVision.Application.Models.Responses.Results;
using CostVision.Domain.Models.Enums.MoneyMovements;
using CostVision.Domain.Models.MoneyMovements;

namespace CostVision.Application.UseCases.MoneyMovements
{
    public class ImportMoneyMovementsUseCase(IUnitOfWork unitOfWork) : IImportMoneyMovementsUseCase
    {
        public async Task<ServiceResult<BankStatementImportResultDto>> ExecuteAsync(SaveBankStatementImportRequest request, Guid currentUserId, CancellationToken ct)
        {
            if (request.AccountId == Guid.Empty)
                return ServiceResult<BankStatementImportResultDto>.Fail(400, "Выберите счёт для импорта.");

            if (request.Rows.Count == 0)
                return ServiceResult<BankStatementImportResultDto>.Fail(400, "Нет строк для импорта.");

            ServiceResult<CostVision.Domain.Models.Receipts.AccountMember> accountAccess = await MoneyMovementAccountAccessValidator.GetEditableAccountMemberAsync(unitOfWork, request.AccountId, currentUserId, ct);
            if (!accountAccess.Success)
                return ServiceResult<BankStatementImportResultDto>.Fail(accountAccess.Error!.StatusCode, accountAccess.Error.Message);

            BankStatementImportResultDto result = new();
            List<ValidImportRow> validRows =
                ValidateRows(request.AccountId, request.Rows, currentUserId, result);
            if (validRows.Count == 0)
                return ServiceResult<BankStatementImportResultDto>.Ok(result);

            await unitOfWork.ExecuteInTransaction(async () =>
            {
                foreach (ValidImportRow validRow in validRows)
                {
                    BankStatementImportRowRequest row = validRow.Row;
                    MoneyMovement movement = validRow.Movement;
                    MoneyMovement? duplicate = await FindDuplicateAsync(request.AccountId, row, movement, ct);
                    if (duplicate != null)
                    {
                        if (!row.ReplaceDuplicate)
                        {
                            AddError(result, validRow.Index, "В импорте есть повторяющаяся операция. Строка пропущена.", row.ImportComment);
                            continue;
                        }

                        if (!duplicate.TryReplaceFromBankStatement(
                                movement.Amount,
                                movement.Type,
                                movement.OccurredAt,
                                movement.Comment,
                                movement.ImportComment,
                                DateTime.UtcNow,
                                out string? error))
                        {
                            AddError(result, validRow.Index, error!, row.ImportComment);
                            continue;
                        }

                        result.UpdatedCount++;
                        continue;
                    }

                    unitOfWork.MoneyMovement.Create(movement);
                    result.CreatedCount++;
                }
            }, ct);

            return ServiceResult<BankStatementImportResultDto>.Ok(result);
        }

        private static List<ValidImportRow> ValidateRows(
            Guid accountId,
            List<BankStatementImportRowRequest> rows,
            Guid currentUserId,
            BankStatementImportResultDto result)
        {
            HashSet<string> keys = new();
            List<ValidImportRow> validRows = new();

            foreach ((BankStatementImportRowRequest row, int index) in rows.Select((row, index) => (row, index)))
            {
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

                BankStatementImportPreviewRowDto previewRow = new()
                {
                    OccurredAt = row.OccurredAt,
                    Amount = row.Amount,
                    Type = row.Type,
                    ImportComment = row.ImportComment
                };

                string key = PreviewBankStatementImportUseCase.BuildRowKey(previewRow);
                if (!keys.Add(key) && !row.ReplaceDuplicate)
                {
                    AddError(result, index, "В попытке импорта есть повторяющаяся строка. Строка пропущена.", row.ImportComment);
                    continue;
                }

                validRows.Add(new ValidImportRow(index, row, movement!));
            }

            return validRows;
        }

        private static void AddError(BankStatementImportResultDto result, int rowIndex, string message, string? rawText)
        {
            result.Errors.Add(new BankStatementImportLineErrorDto
            {
                LineNumber = rowIndex + 1,
                Message = message,
                RawText = rawText ?? string.Empty
            });

            result.ErrorCount = result.Errors.Count;
        }

        private async Task<MoneyMovement?> FindDuplicateAsync(
            Guid accountId,
            BankStatementImportRowRequest row,
            MoneyMovement importedMovement,
            CancellationToken ct)
        {
            if (row.DuplicateMoneyMovementId.HasValue)
            {
                MoneyMovement? duplicateById = await unitOfWork.MoneyMovement.GetItemByPredicateAsync(
                    movement => movement.Id == row.DuplicateMoneyMovementId.Value && movement.AccountId == accountId,
                    asNoTracking: false,
                    ct: ct);

                if (duplicateById != null)
                    return duplicateById;
            }

            return await unitOfWork.MoneyMovement.GetItemByPredicateAsync(
                movement => movement.AccountId == accountId &&
                            movement.Source == MoneyMovementSource.BankStatementImport &&
                            movement.OccurredAt == importedMovement.OccurredAt &&
                            movement.Amount == importedMovement.Amount &&
                            movement.Type == importedMovement.Type &&
                            movement.ImportComment == importedMovement.ImportComment,
                asNoTracking: false,
                ct: ct);
        }

        private readonly record struct ValidImportRow(int Index, BankStatementImportRowRequest Row, MoneyMovement Movement);
    }
}
