using CostVision.Application.Models.Dtos.MoneyMovements;
using CostVision.Domain.Models.MoneyMovements;
using CostVision.Domain.Models.Receipts;
using CostVision.Application.Extensions;

namespace CostVision.Application.UseCases.MoneyMovements.Helpers
{
    internal static class MoneyMovementReceiptMapper
    {
        public static MoneyMovementReceiptDto MapReceiptLinkDto(this Receipt receipt, Guid currentUserId)
        {
            ReceiptAccount? accountLink = receipt.Accounts
                .Where(link => FinancialDataAccess.CanReadAccount(link.Account, currentUserId))
                .OrderBy(link => link.Account!.Name)
                .FirstOrDefault();

            return new MoneyMovementReceiptDto
            {
                ReceiptId = receipt.Id,
                DateTime = receipt.DateTime,
                RetailPlace = receipt.Store?.AdaptiveName ?? receipt.Store?.Name ?? receipt.User ?? "Без названия",
                TotalSum = receipt.TotalSum,
                AccountName = accountLink?.Account?.Name ?? string.Empty,
                CreatedByUserName = receipt.CreatedByUser?.Name ?? string.Empty,
                CreatedAtUtc = receipt.CreatedAtUtc,
                IsLinkedToOtherMoneyMovement = receipt.MoneyMovementLinks.Any(link =>
                    FinancialDataAccess.CanReadAccount(link.MoneyMovement?.Account, currentUserId))
            };
        }

        public static MoneyMovementReceiptDto MapReceiptLinkDto(this MoneyMovementReceipt link, Guid currentUserId)
        {
            MoneyMovementReceiptDto dto = link.Receipt?.MapReceiptLinkDto(currentUserId) ?? new MoneyMovementReceiptDto
            {
                ReceiptId = link.ReceiptId
            };

            dto.IsLinkedToOtherMoneyMovement = true;

            return dto;
        }
    }
}
