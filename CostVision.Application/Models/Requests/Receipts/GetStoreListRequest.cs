namespace CostVision.Application.Models.Requests.Receipts
{
    public class GetStoreListRequest
    {
        public string? Search { get; set; }

        public bool UseAdaptiveNames { get; set; }

        public bool GroupByName { get; set; }

        public int Page { get; set; } = 1;

        public int PageSize { get; set; } = 20;

        public string? SortBy { get; set; }

        public string? SortDirection { get; set; }
    }
}
