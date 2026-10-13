using CostVision.Application.Models.Dtos.MoneyMovements;
using CostVision.Domain.Models.MoneyMovements;
using CostVision.Application.Extensions;

namespace CostVision.Application.UseCases.MoneyMovements.Helpers
{
    internal static class MoneyMovementMapper
    {
        public static MoneyMovementDto MapDto(this MoneyMovement movement, Guid currentUserId, int availableReceiptCount = 0)
        {
            List<MoneyMovementReceipt> visibleLinks = movement.ReceiptLinks
                .Where(link => FinancialDataAccess.CanReadReceipt(link.Receipt, currentUserId))
                .ToList();

            return new MoneyMovementDto
            {
                Id = movement.Id,
                AccountId = movement.AccountId == Guid.Empty ? null : movement.AccountId,
                AccountName = movement.Account?.Name ?? string.Empty,
                AccountColorHex = movement.Account?.ColorHex ?? string.Empty,
                Amount = movement.Amount,
                Type = movement.Type,
                OccurredAt = movement.OccurredAt,
                Comment = movement.Comment,
                ImportComment = movement.ImportComment,
                PerformedByUserId = movement.PerformedByUserId,
                PerformedByUserName = movement.PerformedByUser?.Name ?? string.Empty,
                Source = movement.Source,
                LinkedReceiptCount = visibleLinks.Count,
                AvailableReceiptCount = availableReceiptCount,
                LinkedReceiptsTotalSum = visibleLinks
                    .Sum(link => link.Receipt!.TotalSum)
            };
        }
    }
}
