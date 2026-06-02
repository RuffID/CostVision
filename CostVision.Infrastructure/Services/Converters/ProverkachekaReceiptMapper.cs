using CostVision.Domain.Models.Receipts;
using CostVision.Infrastructure.Models.Responses.ProverkachekaApi;

namespace CostVision.Infrastructure.Services.Converters
{
    public static class ProverkachekaReceiptMapper
    {
        public static Receipt MapToReceipt(this ProverkachekaResponse apiResponse)
        {
            ProverkachekaJson json = apiResponse.Data?.Json ?? throw new InvalidOperationException("JSON part is missing.");

            return new Receipt
            {
                FiscalDriveNumber = json.FiscalDriveNumber ?? string.Empty,
                FiscalDocumentNumber = json.FiscalDocumentNumber.ToString(),
                FiscalSign = json.FiscalSign.ToString(),
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

        public static ReceiptItem MapToReceiptItem(this ProverkachekaItem item, Guid receiptId, Guid productId, Guid? categoryId = null)
        {
            return new ReceiptItem
            {
                ReceiptId = receiptId,
                ProductId = productId,
                CategoryId = categoryId,
                Price = item.Price / 100m,
                Sum = item.Sum / 100m,
                Quantity = item.Quantity,
                Nds = item.Nds,
                PaymentType = item.PaymentType,
                ProductType = item.ProductType,
                ItemsQuantityMeasure = item.ItemsQuantityMeasure
            };
        }
    }
}
