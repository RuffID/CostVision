namespace CostVision.Domain.Models.Enums.Receipts
{
    public enum ReceiptResponseCodeEnum
    {
        Correct = 1,
        Incorrect = 2,
        Pending = 3,
        WaitBeforeRetry = 4,
        RateLimitExceeded = 5,
        Other = 6
    }
}
