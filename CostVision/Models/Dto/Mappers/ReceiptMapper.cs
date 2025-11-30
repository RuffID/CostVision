using CostVision.Models.Receipts;
using CostVision.Models.Responses.ProverkachekaApi;

namespace CostVision.Models.Dto.Mappers
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

                Nds10 = json.Nds10.HasValue ? json.Nds10.Value / 100m : null,
                Nds18 = json.Nds18.HasValue ? json.Nds18.Value / 100m : null,
                Nds0 = json.Nds0.HasValue ? json.Nds0.Value / 100m : null,
                NdsNo = json.NdsNo.HasValue ? json.NdsNo.Value / 100m : null,

                KktRegId = json.KktRegId,
                NumberKkt = json.NumberKkt
            };
        }
    }

}
