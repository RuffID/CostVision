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
        public async Task<ServiceResult<bool>> ExecuteAsync(SaveBankStatementImportRequest request, Guid currentUserId, CancellationToken ct)
        {
            if (request.AccountId == Guid.Empty)
                return ServiceResult<bool>.Fail(400, "Выберите счёт для импорта.");

            if (request.Rows.Count == 0)
                return ServiceResult<bool>.Fail(400, "Нет строк для импорта.");

            ServiceResult<CostVision.Domain.Models.Receipts.AccountMember> accountAccess = await MoneyMovementAccountAccessValidator.GetEditableAccountMemberAsync(unitOfWork, request.AccountId, currentUserId, ct);
            if (!accountAccess.Success)
                return ServiceResult<bool>.Fail(accountAccess.Error!.StatusCode, accountAccess.Error.Message);

            ServiceResult<bool> validationResult = ValidateRows(request.Rows);
            if (!validationResult.Success)
                return validationResult;

            ServiceResult<bool> duplicateActionValidationResult = await ValidateDuplicateActionsAsync(request.AccountId, request.Rows, ct);
            if (!duplicateActionValidationResult.Success)
                return duplicateActionValidationResult;

            await unitOfWork.ExecuteInTransaction(async () =>
            {
                foreach (BankStatementImportRowRequest row in request.Rows)
                {
                    MoneyMovement? duplicate = await FindDuplicateAsync(request.AccountId, row, ct);
                    if (duplicate != null)
                    {
                        if (!row.ReplaceDuplicate)
                            throw new InvalidOperationException("В импорте есть повторяющиеся операции. Удалите их из попытки импорта или выберите замену.");

                        UpdateDuplicate(duplicate, row);
                        continue;
                    }

                    unitOfWork.MoneyMovement.Create(CreateMovement(request.AccountId, row, currentUserId));
                }
            }, ct);

            return ServiceResult<bool>.Ok(true);
        }

        private async Task<ServiceResult<bool>> ValidateDuplicateActionsAsync(Guid accountId, List<BankStatementImportRowRequest> rows, CancellationToken ct)
        {
            foreach (BankStatementImportRowRequest row in rows)
            {
                MoneyMovement? duplicate = await FindDuplicateAsync(accountId, row, ct);
                if (duplicate != null && !row.ReplaceDuplicate)
                    return ServiceResult<bool>.Fail(400, "В импорте есть повторяющиеся операции. Удалите их из попытки импорта или выберите замену.");
            }

            return ServiceResult<bool>.Ok(true);
        }

        private static ServiceResult<bool> ValidateRows(List<BankStatementImportRowRequest> rows)
        {
            HashSet<string> keys = new();

            foreach (BankStatementImportRowRequest row in rows)
            {
                if (row.Amount <= 0)
                    return ServiceResult<bool>.Fail(400, "Сумма импортируемой операции должна быть больше нуля.");

                if (string.IsNullOrWhiteSpace(row.ImportComment))
                    return ServiceResult<bool>.Fail(400, "У импортируемой операции отсутствует исходный комментарий.");

                BankStatementImportPreviewRowDto previewRow = new()
                {
                    OccurredAt = row.OccurredAt,
                    Amount = row.Amount,
                    Type = row.Type,
                    ImportComment = row.ImportComment
                };

                string key = PreviewBankStatementImportUseCase.BuildRowKey(previewRow);
                if (!keys.Add(key) && !row.ReplaceDuplicate)
                    return ServiceResult<bool>.Fail(400, "В попытке импорта есть повторяющиеся строки. Удалите повтор или выберите замену.");
            }

            return ServiceResult<bool>.Ok(true);
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
