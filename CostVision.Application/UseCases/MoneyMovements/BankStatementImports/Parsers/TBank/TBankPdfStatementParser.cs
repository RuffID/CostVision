using CostVision.Application.Models.Dtos.MoneyMovements;
using CostVision.Application.UseCases.MoneyMovements;
using CostVision.Application.UseCases.MoneyMovements.BankStatementImports.Parsing;
using CostVision.Domain.Models.Enums.MoneyMovements;
using System.Globalization;
using System.Text.RegularExpressions;

namespace CostVision.Application.UseCases.MoneyMovements.BankStatementImports.Parsers.TBank
{
    internal class TBankPdfStatementParser : IBankStatementParser
    {
        private static readonly Regex OperationDateRegex = new(@"^\d{2}\.\d{2}\.\d{2}( \d{2}:\d{2})?$", RegexOptions.Compiled);
        private static readonly Regex ProcessingDateRegex = new(@"^\d{2}\.\d{2}\.\d{2}$", RegexOptions.Compiled);
        private static readonly Regex AmountRegex = new(@"^\+?\s?-?[\d ]+([,.]\d{2})\s?₽$", RegexOptions.Compiled);
        private static readonly Regex CombinedOperationRegex = new(
            @"^(?<occurred>\d{2}\.\d{2}\.\d{2} \d{2}:\d{2}|\d{2}\.\d{2}\.\d{2}) (?<processed>\d{2}\.\d{2}\.\d{2}) (?<accountAmount>\+?\s?-?[\d ]+([,.]\d{2})\s?₽)\s*(?<operationAmount>\+?\s?-?[\d ]+([,.]\d{2})\s?₽)(?<description>.*)$",
            RegexOptions.Compiled);
        private static readonly CultureInfo RuCulture = new("ru-RU");

        public string BankId => BankStatementImportConstants.T_BANK_PDF_BANK_ID;

        public string BankName => "Т-Банк";

        public string Description => "PDF-выписка";

        public bool IsConfigured => true;

        public bool CanParseFile(string fileName)
        {
            return fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
        }

        public BankStatementImportPreviewDto Parse(Guid accountId, IReadOnlyList<string> pages)
        {
            List<BankStatementImportPreviewRowDto> rows = new();
            List<BankStatementImportLineErrorDto> errors = new();
            List<ParsedLine> lines = BankStatementTextNormalizer.NormalizePages(pages);
            bool cardBlockStarted = false;

            for (int index = 0; index < lines.Count; index++)
            {
                ParsedLine line = lines[index];

                if (IsCardBlockHeader(line.Text))
                {
                    cardBlockStarted = true;
                    continue;
                }

                if (!cardBlockStarted || IsIgnorableLine(line.Text))
                    continue;

                if (TryParseCombinedOperation(lines, ref index, rows))
                    continue;

                if (!OperationDateRegex.IsMatch(line.Text))
                    continue;

                ParseOperation(lines, ref index, rows, errors);
            }

            return new BankStatementImportPreviewDto
            {
                BankId = BankId,
                AccountId = accountId,
                Rows = rows,
                Errors = errors
            };
        }

        private static void ParseOperation(List<ParsedLine> lines, ref int index, List<BankStatementImportPreviewRowDto> rows, List<BankStatementImportLineErrorDto> errors)
        {
            ParsedLine operationDateLine = lines[index];
            int cursor = index + 1;

            if (!TryGetLine(lines, cursor, out ParsedLine processingDateLine) || !ProcessingDateRegex.IsMatch(processingDateLine.Text))
            {
                AddError(errors, operationDateLine, "Не найдена дата обработки операции.");
                return;
            }

            cursor++;

            if (!TryGetLine(lines, cursor, out ParsedLine accountAmountLine) || !AmountRegex.IsMatch(accountAmountLine.Text))
            {
                AddError(errors, operationDateLine, "Не найдена сумма в валюте счёта.");
                return;
            }

            cursor++;

            if (!TryGetLine(lines, cursor, out ParsedLine operationAmountLine) || !AmountRegex.IsMatch(operationAmountLine.Text))
            {
                AddError(errors, operationDateLine, "Не найдена сумма операции.");
                return;
            }

            cursor++;
            List<string> descriptionLines = new();

            while (TryGetLine(lines, cursor, out ParsedLine descriptionLine) && !IsOperationBoundary(descriptionLine.Text))
            {
                if (!IsIgnorableLine(descriptionLine.Text))
                    descriptionLines.Add(descriptionLine.Text);

                cursor++;
            }

            if (descriptionLines.Count == 0)
            {
                AddError(errors, operationDateLine, "Не найдено описание операции.");
                return;
            }

            DateTime occurredAt = ParseOperationDate(operationDateLine.Text);
            DateTime processedAt = DateTime.ParseExact(processingDateLine.Text, "dd.MM.yy", RuCulture);
            decimal amount = ParseAmount(accountAmountLine.Text);
            MoneyMovementType type = accountAmountLine.Text.TrimStart().StartsWith("+", StringComparison.Ordinal)
                ? MoneyMovementType.Income
                : MoneyMovementType.Expense;
            string importComment = string.Join(' ', descriptionLines).Trim();

            rows.Add(new BankStatementImportPreviewRowDto
            {
                ClientRowId = $"{operationDateLine.Number}-{rows.Count + 1}",
                OccurredAt = occurredAt,
                ProcessedAt = processedAt,
                Amount = amount,
                Type = type,
                Comment = importComment,
                ImportComment = importComment,
                SourceLineNumber = operationDateLine.Number
            });

            index = cursor - 1;
        }

        private static bool TryParseCombinedOperation(List<ParsedLine> lines, ref int index, List<BankStatementImportPreviewRowDto> rows)
        {
            ParsedLine operationLine = lines[index];
            Match match = CombinedOperationRegex.Match(operationLine.Text);
            if (!match.Success)
                return false;

            string description = match.Groups["description"].Value.Trim();
            List<string> descriptionLines = new();

            if (!string.IsNullOrWhiteSpace(description))
                descriptionLines.Add(description);

            int cursor = index + 1;
            while (TryGetLine(lines, cursor, out ParsedLine descriptionLine) && !IsOperationBoundary(descriptionLine.Text))
            {
                if (!IsIgnorableLine(descriptionLine.Text))
                    descriptionLines.Add(descriptionLine.Text);

                cursor++;
            }

            string importComment = string.Join(' ', descriptionLines).Trim();

            rows.Add(new BankStatementImportPreviewRowDto
            {
                ClientRowId = $"{operationLine.Number}-{rows.Count + 1}",
                OccurredAt = ParseOperationDate(match.Groups["occurred"].Value),
                ProcessedAt = DateTime.ParseExact(match.Groups["processed"].Value, "dd.MM.yy", RuCulture),
                Amount = ParseAmount(match.Groups["accountAmount"].Value),
                Type = match.Groups["accountAmount"].Value.TrimStart().StartsWith("+", StringComparison.Ordinal)
                    ? MoneyMovementType.Income
                    : MoneyMovementType.Expense,
                Comment = importComment,
                ImportComment = importComment,
                SourceLineNumber = operationLine.Number
            });

            index = cursor - 1;
            return true;
        }

        private static bool TryGetLine(List<ParsedLine> lines, int index, out ParsedLine line)
        {
            if (index >= 0 && index < lines.Count)
            {
                line = lines[index];
                return true;
            }

            line = new ParsedLine();
            return false;
        }

        private static bool IsOperationBoundary(string line)
        {
            return OperationDateRegex.IsMatch(line) ||
                   CombinedOperationRegex.IsMatch(line) ||
                   IsCardBlockHeader(line) ||
                   IsTableHeaderLine(line) ||
                   IsBlockTotalLine(line);
        }

        private static bool IsIgnorableLine(string line)
        {
            return IsTableHeaderLine(line) || IsBlockTotalLine(line);
        }

        private static bool IsCardBlockHeader(string line)
        {
            return line.StartsWith("Операции по карте №", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTableHeaderLine(string line)
        {
            return line.Contains("Дата и время", StringComparison.OrdinalIgnoreCase) ||
                   line.Equals("операции", StringComparison.OrdinalIgnoreCase) ||
                   line.Equals("Дата", StringComparison.OrdinalIgnoreCase) ||
                   line.Equals("обработки", StringComparison.OrdinalIgnoreCase) ||
                   line.Equals("Сумма", StringComparison.OrdinalIgnoreCase) ||
                   line.Contains("в валюте счёта", StringComparison.OrdinalIgnoreCase) ||
                   line.Equals("Описание", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsBlockTotalLine(string line)
        {
            return line.StartsWith("Расходы:", StringComparison.OrdinalIgnoreCase) ||
                   line.StartsWith("Поступления:", StringComparison.OrdinalIgnoreCase) ||
                   line.StartsWith("Баланс", StringComparison.OrdinalIgnoreCase) ||
                   line.StartsWith("Кэшбэк", StringComparison.OrdinalIgnoreCase);
        }

        private static DateTime ParseOperationDate(string value)
        {
            if (value.Length == 8)
                return DateTime.ParseExact(value, "dd.MM.yy", RuCulture);

            return DateTime.ParseExact(value, "dd.MM.yy HH:mm", RuCulture);
        }

        private static decimal ParseAmount(string value)
        {
            string normalized = value
                .Replace("₽", string.Empty)
                .Replace(" ", string.Empty)
                .Replace("+", string.Empty)
                .Replace(",", ".")
                .Trim();

            return Math.Abs(decimal.Parse(normalized, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture));
        }

        private static void AddError(List<BankStatementImportLineErrorDto> errors, ParsedLine line, string message)
        {
            errors.Add(new BankStatementImportLineErrorDto
            {
                LineNumber = line.Number,
                Message = message,
                RawText = line.Text
            });
        }
    }
}
