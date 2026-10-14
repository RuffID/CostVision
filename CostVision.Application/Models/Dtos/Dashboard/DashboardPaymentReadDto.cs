using CostVision.Domain.Models.Enums.MoneyMovements;

namespace CostVision.Application.Models.Dtos.Dashboard;

public class DashboardPaymentReadDto
{
    public Guid Id { get; set; }
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    public MoneyMovementType Type { get; set; }
}
