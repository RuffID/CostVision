using CostVision.Application.Models.Dtos.Receipts;
using CostVision.Domain.Models.Receipts;

namespace CostVision.Application.Models.Dtos.Mappers
{
    public static class ReceiptGroupMapper
    {
        public static ReceiptDto MapReceiptGroupDto(this IEnumerable<Receipt> receipts, Guid currentUserId)
        {
            List<ReceiptDto> groupedReceipts = receipts
                .Select(receipt => receipt.MapReceiptDto(currentUserId))
                .ToList();

            ReceiptDto representativeReceipt = groupedReceipts
                .OrderByDescending(receipt => receipt.Accounts.Any(account => account.CanEditReceipt))
                .First();

            List<ReceiptAccountDto> mergedAccounts = groupedReceipts
                .SelectMany(receipt => receipt.Accounts)
                .GroupBy(account => account.Id)
                .Select(group => group
                    .OrderByDescending(account => account.CanEditReceipt)
                    .First())
                .OrderBy(account => account.Name)
                .ToList();

            representativeReceipt.Accounts = mergedAccounts;

            ReceiptAccountDto? primaryAccount = mergedAccounts.FirstOrDefault();
            ReceiptAccountDto? primaryEditableAccount = mergedAccounts.FirstOrDefault(account => account.CanEditReceipt);

            representativeReceipt.Id = primaryEditableAccount?.ReceiptId ?? primaryAccount?.ReceiptId ?? representativeReceipt.Id;
            representativeReceipt.AccountId = primaryAccount?.Id;
            representativeReceipt.AccountName = primaryAccount?.Name ?? string.Empty;

            return representativeReceipt;
        }
    }
}
