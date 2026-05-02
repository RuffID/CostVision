using CostVision.Domain.Models.Enums.Authorization;
using CostVision.Domain.Models.Receipts;
using CostVision.Application.Models.Dtos.Receipts;

namespace CostVision.Application.Models.Dtos.Mappers
{
    public static class ReceiptMapper
    {
        public static ReceiptDto MapReceiptDto(this Receipt receipt)
        {
            return receipt.MapReceiptDto(receipt.CreatedByUserId);
        }

        public static ReceiptDto MapReceiptDto(this Receipt receipt, Guid currentUserId)
        {
            List<ReceiptAccountDto> accounts = receipt.Accounts
                .Where(x => x.Account != null)
                .GroupBy(x => x.AccountId)
                .Select(group => new ReceiptAccountDto
                {
                    Id = group.Key,
                    ReceiptId = receipt.Id,
                    Name = group.Select(x => x.Account!.Name).FirstOrDefault() ?? string.Empty,
                    ColorHex = group.Select(x => x.Account!.ColorHex).FirstOrDefault() ?? Account.DEFAULT_COLOR_HEX,
                    CanEditReceipt = receipt.CreatedByUserId == currentUserId
                        && group.Any(x =>
                            x.Account != null &&
                            (x.Account.CreatedByUserId == currentUserId ||
                             x.Account.Members.Any(member =>
                                 member.UserId == currentUserId &&
                                 (member.Role == AccountAccessRole.Owner || member.Role == AccountAccessRole.Editor))))
                })
                .OrderBy(x => x.Name)
                .ToList();

            ReceiptAccountDto? receiptAccount = accounts.FirstOrDefault();

            return new ReceiptDto
            {
                Id = receipt.Id,
                DateTime = receipt.DateTime,
                RetailPlace = receipt.RetailPlace ?? receipt.User ?? string.Empty,
                RetailPlaceAddress = receipt.RetailPlaceAddress ?? string.Empty,
                FiscalDocumentNumber = receipt.FiscalDocumentNumber ?? string.Empty,
                FiscalDriveNumber = receipt.FiscalDriveNumber ?? string.Empty,
                FiscalSign = receipt.FiscalSign ?? string.Empty,
                TotalSum = receipt.TotalSum,
                AccountId = receiptAccount?.Id,
                AccountName = receiptAccount?.Name ?? string.Empty,
                Accounts = accounts
            };
        }
    }
}
