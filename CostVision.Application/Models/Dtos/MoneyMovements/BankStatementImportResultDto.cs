namespace CostVision.Application.Models.Dtos.MoneyMovements
{
    /// <summary>
    /// Результат сохранения строк импорта банковской выписки.
    /// </summary>
    public class BankStatementImportResultDto
    {
        public int CreatedCount { get; set; }

        public int UpdatedCount { get; set; }

        public int ErrorCount { get; set; }

        public List<BankStatementImportLineErrorDto> Errors { get; set; } = new();
    }
}
