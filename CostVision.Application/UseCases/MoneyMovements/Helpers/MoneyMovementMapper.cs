using CostVision.Application.Models.Dtos.MoneyMovements;
using CostVision.Domain.Models.MoneyMovements;

namespace CostVision.Application.UseCases.MoneyMovements.Helpers
{
    internal static class MoneyMovementMapper
    {
        public static MoneyMovementDto MapDto(this MoneyMovement movement)
        {
            return new MoneyMovementDto
            {
                Id = movement.Id,
                AccountId = movement.AccountId,
                AccountName = movement.Account?.Name ?? string.Empty,
                AccountColorHex = movement.Account?.ColorHex ?? string.Empty,
                Amount = movement.Amount,
                Type = movement.Type,
                OccurredAt = movement.OccurredAt,
                Comment = movement.Comment,
                PerformedByUserId = movement.PerformedByUserId,
                PerformedByUserName = movement.PerformedByUser?.Name ?? string.Empty,
                Source = movement.Source
            };
        }
    }
}
