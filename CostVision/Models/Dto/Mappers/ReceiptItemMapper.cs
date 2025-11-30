using CostVision.Models.Products;
using CostVision.Models.Receipts;
using CostVision.Models.Responses.ProverkachekaApi;

namespace CostVision.Models.Dto.Mappers
{
    public static class ReceiptItemMapper
    {
        public static ReceiptItem MapToReceiptItem(this ProverkachekaItem item, Receipt receipt, Product product)
        {
            return new()
            {
                Receipt = receipt,
                Product = product,
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
