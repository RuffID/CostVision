namespace CostVision.Application.Models.Dtos;

public class ReadQueryTotals
{
    public int Count { get; set; }
    public decimal Income { get; set; }
    public decimal Expense { get; set; }
    public int InvalidReceiptOperationCount { get; set; }
}
