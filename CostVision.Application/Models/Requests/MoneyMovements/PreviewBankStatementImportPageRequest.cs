namespace CostVision.Application.Models.Requests.MoneyMovements
{
    public class PreviewBankStatementImportPageRequest
    {
        public string BankId { get; set; } = string.Empty;

        public Guid AccountId { get; set; }

        public string FileName { get; set; } = string.Empty;

        public Stream? FileStream { get; set; }

        public long FileLength { get; set; }
    }
}
