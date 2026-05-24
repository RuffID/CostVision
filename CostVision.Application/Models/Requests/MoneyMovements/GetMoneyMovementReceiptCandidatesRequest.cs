namespace CostVision.Application.Models.Requests.MoneyMovements
{
    public class GetMoneyMovementReceiptCandidatesRequest
    {
        public Guid MoneyMovementId { get; set; }

        public bool UseTimeWindow { get; set; } = true;

        public DateTime? DateFrom { get; set; }

        public DateTime? DateTo { get; set; }

        public bool UseAmountFilter { get; set; } = true;

        public decimal? AmountTolerance { get; set; }

        public bool ExcludeLinkedReceipts { get; set; } = true;
    }
}
