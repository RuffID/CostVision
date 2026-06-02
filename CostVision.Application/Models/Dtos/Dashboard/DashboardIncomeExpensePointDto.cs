namespace CostVision.Application.Models.Dtos.Dashboard
{
    public class DashboardIncomeExpensePointDto
    {
        public DateTime PeriodStart { get; set; }
        public string Label { get; set; } = string.Empty;
        public decimal IncomeSum { get; set; }
        public decimal ExpenseSum { get; set; }
    }
}
