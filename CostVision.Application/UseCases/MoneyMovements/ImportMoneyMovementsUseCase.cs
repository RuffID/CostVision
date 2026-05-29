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
            List<BankStatementImportRowRequest> validRows = ValidateRows(request.Rows, result);
            if (validRows.Count == 0)
                return ServiceResult<BankStatementImportResultDto>.Ok(result);

            await unitOfWork.ExecuteInTransaction(async () =>
            {
                foreach (BankStatementImportRowRequest row in validRows)
                {
                    MoneyMovement? duplicate = await FindDuplicateAsync(request.AccountId, row, ct);
                    if (duplicate != null)
                    {
                        if (!row.ReplaceDuplicate)
                        {
                            AddError(result, request.Rows.IndexOf(row), "В импорте есть повторяющаяся операция. Строка пропущена.", row.ImportComment);
                            continue;
                        }

                        UpdateDuplicate(duplicate, row);
                        result.UpdatedCount++;
                        continue;
                    }

                    unitOfWork.MoneyMovement.Create(CreateMovement(request.AccountId, row, currentUserId));
                    result.CreatedCount++;
                }
            }, ct);

            return ServiceResult<BankStatementImportResultDto>.Ok(result);
        }

        private static List<BankStatementImportRowRequest> ValidateRows(List<BankStatementImportRowRequest> rows, BankStatementImportResultDto result)
        {
            HashSet<string> keys = new();
            List<BankStatementImportRowRequest> validRows = new();

            foreach ((BankStatementImportRowRequest row, int index) in rows.Select((row, index) => (row, index)))
            {
                if (row.Amount <= 0)
                {
                    AddError(result, index, "Сумма импортируемой операции должна быть больше нуля.", row.ImportComment);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(row.ImportComment))
                {
                    AddError(result, index, "У импортируемой операции отсутствует исходный комментарий.", row.ImportComment);
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

                validRows.Add(row);
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

        private async Task<MoneyMovement?> FindDuplicateAsync(Guid accountId, BankStatementImportRowRequest row, CancellationToken ct)
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
                            movement.OccurredAt == row.OccurredAt &&
                            movement.Amount == row.Amount &&
                            movement.Type == row.Type &&
                            movement.ImportComment == row.ImportComment,
                asNoTracking: false,
                ct: ct);
        }

        private static MoneyMovement CreateMovement(Guid accountId, BankStatementImportRowRequest row, Guid currentUserId)
        {
            DateTime nowUtc = DateTime.UtcNow;

            return new MoneyMovement
            {
                AccountId = accountId,
                Amount = Math.Abs(row.Amount),
                Type = row.Type,
                OccurredAt = row.OccurredAt,
                Comment = NormalizeComment(row.Comment),
                ImportComment = row.ImportComment.Trim(),
                CreatedByUserId = currentUserId,
                PerformedByUserId = currentUserId,
                CreatedAtUtc = nowUtc,
                Source = MoneyMovementSource.BankStatementImport
            };
        }

        private static void UpdateDuplicate(MoneyMovement duplicate, BankStatementImportRowRequest row)
        {
            duplicate.Amount = Math.Abs(row.Amount);
            duplicate.Type = row.Type;
            duplicate.OccurredAt = row.OccurredAt;
            duplicate.Comment = NormalizeComment(row.Comment);
            duplicate.ImportComment = row.ImportComment.Trim();
            duplicate.Source = MoneyMovementSource.BankStatementImport;
            duplicate.MarkUpdated(DateTime.UtcNow);
        }

        private static string? NormalizeComment(string? comment)
        {
            return string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        }
    }
}
