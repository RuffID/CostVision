namespace CostVision.Application.Models.Requests.Receipts
{
    public class UpdateStoreAdaptiveNameRequest
    {
        public Guid StoreId { get; set; }

        public string? AdaptiveName { get; set; }
    }
}
