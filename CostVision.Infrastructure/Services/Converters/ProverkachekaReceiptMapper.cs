using CostVision.Domain.Models.Receipts;
using CostVision.Infrastructure.Models.Responses.ProverkachekaApi;

namespace CostVision.Infrastructure.Services.Converters
{
    public static class ProverkachekaReceiptMapper
    {
        public static Receipt MapToReceipt(this ProverkachekaResponse apiResponse, Guid createdByUserId, DateTime createdAtUtc)
        {
            ProverkachekaJson json = apiResponse.Data?.Json ?? throw new InvalidOperationException("JSON part is missing.");

            if (!Receipt.TryCreate(
                    json.FiscalDriveNumber,
                    json.FiscalDocumentNumber.ToString(),
                    json.FiscalSign.ToString(),
                    json.DateTime,
                    json.OperationType,
                    json.TotalSum / 100m,
                    createdByUserId,
                    createdAtUtc,
                    out Receipt? receipt,
                    out string? creationError))
                throw new InvalidOperationException($"Receipt data is invalid: {creationError}");

            if (!receipt!.TryUpdateDetails(
                    json.User,
                    json.UserInn,
                    json.RequestNumber,
                    json.ShiftNumber,
                    Enum.IsDefined(json.AppliedTaxationType) ? json.AppliedTaxationType : null,
                    json.CashTotalSum / 100m,
                    json.EcashTotalSum / 100m,
                    json.Nds18.HasValue ? json.Nds18.Value / 100m : null,
                    json.Nds10.HasValue ? json.Nds10.Value / 100m : null,
                    json.Nds0.HasValue ? json.Nds0.Value / 100m : null,
                    json.NdsNo.HasValue ? json.NdsNo.Value / 100m : null,
                    json.KktRegId,
                    json.NumberKkt,
                    json.Region,
                    out string? detailsError))
                throw new InvalidOperationException($"Receipt details are invalid: {detailsError}");

            return receipt;
        }

        public static ReceiptItem MapToReceiptItem(this ProverkachekaItem item, Product product, Guid? categoryId = null)
        {
            if (!ReceiptItem.TryCreate(
                    item.Price / 100m,
                    item.Quantity,
                    item.Sum / 100m,
                    item.Nds,
                    item.PaymentType,
                    item.ProductType,
                    item.ItemsQuantityMeasure,
                    product,
                    categoryId,
                    out ReceiptItem? receiptItem,
                    out string? error))
                throw new InvalidOperationException($"Receipt item data is invalid: {error}");

            return receiptItem!;
        }
    }
}
