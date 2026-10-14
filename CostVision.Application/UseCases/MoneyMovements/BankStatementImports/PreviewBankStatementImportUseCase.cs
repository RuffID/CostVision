using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.Abstractions.DataBase.Repositories.MoneyMovements;
using CostVision.Application.Abstractions.Service.MoneyMovements;
using CostVision.Application.Models.Dtos.MoneyMovements;
using CostVision.Application.Models.Requests.MoneyMovements;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.MoneyMovements.BankStatementImports.Parsing;
using CostVision.Domain.Models.Enums.MoneyMovements;
using CostVision.Domain.Models.MoneyMovements;
using CostVision.Domain.Models.Receipts;

namespace CostVision.Application.UseCases.MoneyMovements
{
    public class PreviewBankStatementImportUseCase(
        IMoneyMovementRepository moneyMovementRepository, IAccountMemberRepository accountMemberRepository,
        IBankStatementParserRegistry parserRegistry,
        IBankStatementPdfTextExtractor pdfTextExtractor) : IPreviewBankStatementImportUseCase
    {
        public async Task<ServiceResult<BankStatementImportPreviewDto>> ExecuteAsync(PreviewBankStatementImportRequest request, Guid currentUserId, CancellationToken ct)
        {
            if (request.FileStream == null || request.FileLength == 0)
            {
                return ServiceResult<BankStatementImportPreviewDto>.Fail(ServiceErrorType.Validation, "Выберите файл выписки.");
            }

            IBankStatementParser? parser = parserRegistry.FindByBankId(request.BankId);
            if (parser == null || !parser.IsConfigured)
            {
                return ServiceResult<BankStatementImportPreviewDto>.Fail(ServiceErrorType.Validation, "Для выбранного банка импорт пока не настроен.");
            }

            if (!parser.CanParseFile(request.FileName))
            {
                return ServiceResult<BankStatementImportPreviewDto>.Fail(ServiceErrorType.Validation, $"Загрузите файл выписки в формате: {parser.Description}.");
            }

            ServiceResult<AccountMember> accountAccess = await MoneyMovementAccountAccessValidator.GetEditableAccountMemberAsync(accountMemberRepository, request.AccountId, currentUserId, ct);
            if (!accountAccess.Success)
            {
                return accountAccess.PropagateFailure<BankStatementImportPreviewDto>();
            }

            IReadOnlyList<string> pages = await pdfTextExtractor.ExtractPagesAsync(request.FileStream, ct);
            if (pages.Count == 0 || pages.All(string.IsNullOrWhiteSpace))
            {
                return ServiceResult<BankStatementImportPreviewDto>.Fail(ServiceErrorType.Validation, "В PDF не найден текстовый слой. Загрузите выписку с распознаваемым текстом.");
            }

            BankStatementImportPreviewDto preview = parser.Parse(request.AccountId, pages);
            if (preview.Rows.Count == 0 && preview.Errors.Count == 0)
            {
                return ServiceResult<BankStatementImportPreviewDto>.Fail(ServiceErrorType.Validation, BuildEmptyPreviewMessage(pages));
            }

            MarkDuplicates(preview, await LoadExistingImportedMovementsAsync(request.AccountId, preview.Rows, ct));
            MarkPreviewDuplicates(preview);

            return ServiceResult<BankStatementImportPreviewDto>.Ok(preview);
        }

        private async Task<List<MoneyMovement>> LoadExistingImportedMovementsAsync(Guid accountId, List<BankStatementImportPreviewRowDto> rows, CancellationToken ct)
        {
            if (rows.Count == 0)
                return new List<MoneyMovement>();

            DateTime minDate = rows.Min(row => row.OccurredAt).Date;
            DateTime maxDate = rows.Max(row => row.OccurredAt).Date.AddDays(1);

            return await moneyMovementRepository.GetItemsByPredicateAsync(
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
            Dictionary<BankStatementRowKey, List<MoneyMovement>> movementsByKey = existingMovements
                .GroupBy(BankStatementRowKey.From).ToDictionary(group => group.Key, group => group.ToList());

            foreach (BankStatementImportPreviewRowDto row in preview.Rows)
            {
                if (!movementsByKey.TryGetValue(BuildRowKey(row), out List<MoneyMovement>? duplicates))
                {
                    continue;
                }

                row.IsDuplicate = true;
                if (duplicates.Count == 1)
                {
                    row.DuplicateMoneyMovementId = duplicates[0].Id;
                }
            }
        }

        private static void MarkPreviewDuplicates(BankStatementImportPreviewDto preview)
        {
            HashSet<BankStatementRowKey> seenRows = new();

            foreach (BankStatementImportPreviewRowDto row in preview.Rows)
            {
                if (seenRows.Add(BuildRowKey(row)))
                {
                    continue;
                }

                row.IsDuplicate = true;
            }
        }

        internal static bool IsSameImportedOperation(MoneyMovement movement, BankStatementImportPreviewRowDto row)
        {
            return BankStatementRowKey.From(movement) == BuildRowKey(row);
        }

        internal static BankStatementRowKey BuildRowKey(BankStatementImportPreviewRowDto row)
        {
            return new(row.OccurredAt, row.Amount, row.Type, row.ImportComment.Trim());
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

    }
}
