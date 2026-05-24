namespace CostVision.Application.Models.Dtos.MoneyMovements
{
    public class BankStatementImportPreviewDto
    {
        public string BankId { get; set; } = string.Empty;

        public Guid AccountId { get; set; }

        public List<BankStatementImportPreviewRowDto> Rows { get; set; } = new();

        public List<BankStatementImportLineErrorDto> Errors { get; set; } = new();
    }
}
