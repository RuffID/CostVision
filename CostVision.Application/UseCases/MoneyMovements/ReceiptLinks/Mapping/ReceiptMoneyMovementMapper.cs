using CostVision.Application.Models.Dtos.MoneyMovements;
using CostVision.Domain.Models.MoneyMovements;

namespace CostVision.Application.UseCases.MoneyMovements.Helpers
{
    internal static class ReceiptMoneyMovementMapper
    {
        public static ReceiptMoneyMovementDto MapReceiptMoneyMovementDto(this MoneyMovement movement)
        {
            return new ReceiptMoneyMovementDto
            {
                MoneyMovementId = movement.Id,
                OccurredAt = movement.OccurredAt,
                Amount = movement.Amount,
                Type = movement.Type,
                Comment = movement.Comment ?? string.Empty,
                ImportComment = movement.ImportComment ?? string.Empty,
                AccountName = movement.Account?.Name ?? string.Empty,
                IsLinkedToOtherReceipt = movement.ReceiptLinks.Any()
            };
        }

        public static ReceiptMoneyMovementDto MapReceiptMoneyMovementDto(this MoneyMovementReceipt link)
        {
            ReceiptMoneyMovementDto dto = link.MoneyMovement?.MapReceiptMoneyMovementDto() ?? new ReceiptMoneyMovementDto
            {
                MoneyMovementId = link.MoneyMovementId
            };

            dto.IsLinkedToOtherReceipt = true;

            return dto;
        }
    }
}
