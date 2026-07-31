using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.Service.MoneyMovements;
using CostVision.Application.Models.Dtos.MoneyMovements;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.MoneyMovements.BankStatementImports.Parsing;
using CostVision.Domain.Models.Enums.MoneyMovements;
using CostVision.Domain.Models.MoneyMovements;

namespace CostVision.Application.UseCases.MoneyMovements
{
    public class PreviewBankStatementImportUseCase(
        IUnitOfWork unitOfWork,
        IBankStatementParserRegistry parserRegistry,
        IBankStatementPdfTextExtractor pdfTextExtractor) : IPreviewBankStatementImportUseCase
    {
        public async Task<ServiceResult<BankStatementImportPreviewDto>> ExecuteAsync(string bankId, Guid accountId, string fileName, Stream fileStream, Guid currentUserId, CancellationToken ct)
        {
            IBankStatementParser? parser = parserRegistry.FindByBankId(bankId);
            if (parser == null || !parser.IsConfigured)
                return ServiceResult<BankStatementImportPreviewDto>.Fail(400, "Для выбранного банка импорт пока не настроен.");

            if (!parser.CanParseFile(fileName))
                return ServiceResult<BankStatementImportPreviewDto>.Fail(400, $"Загрузите файл выписки в формате: {parser.Description}.");

            ServiceResult<CostVision.Domain.Models.Receipts.AccountMember> accountAccess = await MoneyMovementAccountAccessValidator.GetEditableAccountMemberAsync(unitOfWork, accountId, currentUserId, ct);
            if (!accountAccess.Success)
                return ServiceResult<BankStatementImportPreviewDto>.Fail(accountAccess.Error!.StatusCode, accountAccess.Error.Message);

            IReadOnlyList<string> pages = await pdfTextExtractor.ExtractPagesAsync(fileStream, ct);
            if (pages.Count == 0 || pages.All(string.IsNullOrWhiteSpace))
                return ServiceResult<BankStatementImportPreviewDto>.Fail(400, "В PDF не найден текстовый слой. Загрузите выписку с распознаваемым текстом.");

            BankStatementImportPreviewDto preview = parser.Parse(accountId, pages);
            if (preview.Rows.Count == 0 && preview.Errors.Count == 0)
                return ServiceResult<BankStatementImportPreviewDto>.Fail(400, BuildEmptyPreviewMessage(pages));

            MarkDuplicates(preview, await LoadExistingImportedMovementsAsync(accountId, preview.Rows, ct));
            MarkPreviewDuplicates(preview);

            return ServiceResult<BankStatementImportPreviewDto>.Ok(preview);
        }

        private async Task<List<MoneyMovement>> LoadExistingImportedMovementsAsync(Guid accountId, List<BankStatementImportPreviewRowDto> rows, CancellationToken ct)
        {
            if (rows.Count == 0)
                return new List<MoneyMovement>();

            DateTime minDate = rows.Min(row => row.OccurredAt).Date;
            DateTime maxDate = rows.Max(row => row.OccurredAt).Date.AddDays(1);

            return await unitOfWork.MoneyMovement.GetItemsByPredicateAsync(
                movement => movement.AccountId == accountId &&
                            movement.Source == MoneyMovementSource.BankStatementImport &&
                            movement.ImportComment != null &&
                            movement.OccurredAt >= minDate &&
                            movement.OccurredAt < maxDate,
                asNoTracking: true,
                ct: ct);
        }

        private static void MarkDuplicates(BankStatementImportPreviewDto preview, List<MoneyMovement> existingMovements)
        {
            Dictionary<ImportedOperationKey, MoneyMovement> movementsByKey = new();
            foreach (MoneyMovement movement in existingMovements)
                movementsByKey.TryAdd(GetImportedOperationKey(movement), movement);

            foreach (BankStatementImportPreviewRowDto row in preview.Rows)
            {
                if (!movementsByKey.TryGetValue(GetImportedOperationKey(row), out MoneyMovement? duplicate))
                    continue;

                row.IsDuplicate = true;
                row.DuplicateMoneyMovementId = duplicate.Id;
            }
        }

        private static void MarkPreviewDuplicates(BankStatementImportPreviewDto preview)
        {
            Dictionary<string, BankStatementImportPreviewRowDto> seenRows = new();

            foreach (BankStatementImportPreviewRowDto row in preview.Rows)
            {
                string key = BuildRowKey(row);
                if (seenRows.TryAdd(key, row))
                    continue;

                row.IsDuplicate = true;
            }
        }

        internal static bool IsSameImportedOperation(MoneyMovement movement, BankStatementImportPreviewRowDto row)
        {
            return movement.OccurredAt == row.OccurredAt &&
                   movement.Amount == row.Amount &&
                   movement.Type == row.Type &&
                   string.Equals(movement.ImportComment, row.ImportComment, StringComparison.Ordinal);
        }

        internal static string BuildRowKey(BankStatementImportPreviewRowDto row)
        {
            return $"{row.OccurredAt:O}|{row.Amount}|{(int)row.Type}|{row.ImportComment}";
        }

        private static ImportedOperationKey GetImportedOperationKey(MoneyMovement movement)
        {
            return new ImportedOperationKey(movement.OccurredAt, movement.Amount, movement.Type, movement.ImportComment);
        }

        private static ImportedOperationKey GetImportedOperationKey(BankStatementImportPreviewRowDto row)
        {
            return new ImportedOperationKey(row.OccurredAt, row.Amount, row.Type, row.ImportComment);
        }

        private static string BuildEmptyPreviewMessage(IReadOnlyList<string> pages)
        {
            List<string> lines = pages
                .SelectMany(page => page.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Take(40)
                .ToList();

            if (lines.Count == 0)
                return "Не удалось распознать операции в выписке. Pdf-текст извлечён, но после нормализации не осталось непустых строк.";

            return "Не удалось распознать операции в выписке. Первые строки, извлечённые из PDF: " + string.Join(" | ", lines);
        }

        private readonly record struct ImportedOperationKey(DateTime OccurredAt, decimal Amount, MoneyMovementType Type, string? ImportComment);
    }
}
