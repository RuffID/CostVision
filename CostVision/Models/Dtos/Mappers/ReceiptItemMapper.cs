using CostVision.Models.Receipts;
using CostVision.Models.Responses.ProverkachekaApi;

namespace CostVision.Models.Dtos.Mappers
{
    public static class ReceiptItemMapper
    {
        public static ReceiptItem MapToReceiptItem(this ProverkachekaItem item, Guid receiptId, Guid productId, Guid? categoryId = null)
        {
            return new()
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
