using CostVision.Application.Models.Dtos.MoneyMovements;
using CostVision.Domain.Models.MoneyMovements;
using CostVision.Domain.Models.Receipts;

namespace CostVision.Application.UseCases.MoneyMovements.Helpers
{
    internal static class MoneyMovementReceiptMapper
    {
        public static MoneyMovementReceiptDto MapReceiptLinkDto(this Receipt receipt)
        {
            ReceiptAccount? accountLink = receipt.Accounts
                .Where(link => link.Account != null)
                .OrderBy(link => link.Account!.Name)
                .FirstOrDefault();

            return new MoneyMovementReceiptDto
            {
                ReceiptId = receipt.Id,
                DateTime = receipt.DateTime,
                RetailPlace = receipt.RetailPlace ?? receipt.User ?? "Без названия",
                TotalSum = receipt.TotalSum,
                AccountName = accountLink?.Account?.Name ?? string.Empty,
                IsLinkedToOtherMoneyMovement = receipt.MoneyMovementLinks.Any()
            };
        }

        public static MoneyMovementReceiptDto MapReceiptLinkDto(this MoneyMovementReceipt link)
        {
            MoneyMovementReceiptDto dto = link.Receipt?.MapReceiptLinkDto() ?? new MoneyMovementReceiptDto
            {
                ReceiptId = link.ReceiptId
            };

            dto.IsLinkedToOtherMoneyMovement = true;

            return dto;
        }
    }
}
