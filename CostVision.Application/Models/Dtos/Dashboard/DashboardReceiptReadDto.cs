using CostVision.Domain.Models.Receipts;

namespace CostVision.Application.Models.Dtos.Dashboard;

public class DashboardReceiptReadDto
{
    public ReceiptIdentity Identity { get; set; } = null!;
    public List<Guid> MovementIds { get; set; } = [];
}
