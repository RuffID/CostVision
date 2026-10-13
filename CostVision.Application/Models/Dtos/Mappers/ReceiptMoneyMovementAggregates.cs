using CostVision.Application.Extensions;
using CostVision.Application.Models.Dtos.Receipts;
using CostVision.Domain.Models.MoneyMovements;
using CostVision.Domain.Models.Receipts;

namespace CostVision.Application.Models.Dtos.Mappers;

internal static class ReceiptMoneyMovementAggregates
{
    public static void Apply(ReceiptDto dto, IEnumerable<Receipt> receipts, Guid currentUserId)
    {
        List<MoneyMovement> movements = receipts.SelectMany(receipt => receipt.MoneyMovementLinks)
            .Where(link => FinancialDataAccess.CanReadAccount(link.MoneyMovement?.Account, currentUserId))
            .Select(link => link.MoneyMovement!)
            .DistinctBy(movement => movement.Id).ToList();
        dto.MoneyMovementCount = movements.Count;
        dto.MoneyMovementsTotalSum = movements.Sum(movement => movement.Amount);
    }
}
