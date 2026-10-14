namespace CostVision.Application.Models.Dtos.MoneyMovements;

public class MoneyMovementListDto : PagedListDto<MoneyMovementDto>
{
    public decimal IncomeSum { get; set; }
    public decimal ExpenseSum { get; set; }
}
