namespace CostVision.Application.Models.Dtos.MoneyMovements
{
    public class BankStatementImportLineErrorDto
    {
        public int LineNumber { get; set; }

        public string Message { get; set; } = string.Empty;

        public string RawText { get; set; } = string.Empty;
    }
}
