namespace CostVision.Application.Models.Requests.MoneyMovements
{
    public class GetReceiptMoneyMovementCandidatesRequest
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;

        public Guid ReceiptId { get; set; }

        public bool UseTimeWindow { get; set; }

        public decimal? TimeWindowHours { get; set; }

        public DateTime? DateFrom { get; set; }

        public DateTime? DateTo { get; set; }

        public bool UseAmountFilter { get; set; } = true;

        public decimal? AmountTolerance { get; set; }

        public bool ExcludeLinkedMoneyMovements { get; set; } = true;
    }
}
