using System.Text.RegularExpressions;

namespace CostVision.Application.UseCases.MoneyMovements.BankStatementImports.Parsing
{
    internal static class BankStatementTextNormalizer
    {
        public static List<ParsedLine> NormalizePages(IReadOnlyList<string> pages)
        {
            List<ParsedLine> lines = new();
            int lineNumber = 1;

            foreach (string page in pages)
            {
                string[] pageLines = page.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
                foreach (string rawLine in pageLines)
                {
                    string normalized = Regex.Replace(rawLine.Trim(), @"\s+", " ");
                    if (!string.IsNullOrWhiteSpace(normalized))
                    {
                        lines.Add(new ParsedLine
                        {
                            Number = lineNumber,
                            Text = normalized
                        });
                    }

                    lineNumber++;
                }
            }

            return lines;
        }
    }
}
