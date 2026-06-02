namespace CostVision.Application.Models.Requests.Dashboard
{
    public class DashboardIncomeExpenseReportRequest
    {
        public DateTime DateFrom { get; set; }
        public DateTime DateTo { get; set; }
        public string Period { get; set; } = string.Empty;
        public string ExpenseSource { get; set; } = string.Empty;
        public List<Guid> AccountIds { get; set; } = new();
        public List<string> StoreNames { get; set; } = new();
    }
}
