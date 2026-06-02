namespace CostVision.Application.Models.Dtos.Dashboard
{
    public class DashboardIncomeExpenseReportDto
    {
        public List<string> Stores { get; set; } = new();
        public List<DashboardIncomeExpensePointDto> Points { get; set; } = new();
    }
}
