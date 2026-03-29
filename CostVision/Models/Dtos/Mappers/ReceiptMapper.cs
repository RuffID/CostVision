using CostVision.Models.Dtos.Receipts;
using CostVision.Models.Enums.Authorization;
using CostVision.Models.Receipts;
using CostVision.Models.Responses.ProverkachekaApi;

namespace CostVision.Models.Dtos.Mappers
{
    public static class ReceiptMapper
    {
        public static Receipt MapToReceipt(this ProverkachekaResponse apiResponse)
        {
            ProverkachekaJson json = apiResponse.Data?.Json ?? throw new InvalidOperationException("JSON part is missing.");

            return new Receipt()
            {
                FiscalDriveNumber = json.FiscalDriveNumber ?? string.Empty,
                FiscalDocumentNumber = json.FiscalDocumentNumber.ToString(),
                FiscalSign = json.FiscalSign.ToString(),

                RetailPlace = json.RetailPlace,
                RetailPlaceAddress = json.RetailPlaceAddress,
                User = json.User,
                UserInn = json.UserInn,
                Region = json.Region,

                DateTime = json.DateTime,
                CheckNumber = json.RequestNumber,
                ShiftNumber = json.ShiftNumber,

                OperationType = json.OperationType,
                TaxationType = json.AppliedTaxationType,

                TotalSum = json.TotalSum / 100m,
                CashTotalSum = json.CashTotalSum / 100m,
                EcashTotalSum = json.EcashTotalSum / 100m,

                Nds18 = json.Nds18.HasValue ? json.Nds18.Value / 100m : null,
                Nds10 = json.Nds10.HasValue ? json.Nds10.Value / 100m : null,
                Nds0 = json.Nds0.HasValue ? json.Nds0.Value / 100m : null,
                NdsNo = json.NdsNo.HasValue ? json.NdsNo.Value / 100m : null,

                KktRegId = json.KktRegId,
                NumberKkt = json.NumberKkt
            };
        }

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
                                 member.Role == AccountAccessRole.Owner || member.Role == AccountAccessRole.Editor)))
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
