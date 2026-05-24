namespace CostVision.Application.Models.Requests.MoneyMovements
{
    public class SaveBankStatementImportRequest
    {
        public Guid AccountId { get; set; }

        public List<BankStatementImportRowRequest> Rows { get; set; } = new();
    }
}
