namespace CostVision.Application.Models.Requests.Receipts
{
    public class UpdateProductAdaptiveNameRequest
    {
        public Guid ProductId { get; set; }

        public string? AdaptiveName { get; set; }
    }
}
