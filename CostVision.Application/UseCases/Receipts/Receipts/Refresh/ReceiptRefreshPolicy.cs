namespace CostVision.Application.UseCases.Receipts.Receipts.Refresh
{
    public static class ReceiptRefreshPolicy
    {
        public const int MAX_ATTEMPTS = 7;

        public static DateTime GetNextDailyRunUtc(TimeProvider timeProvider)
        {
            DateTime nextLocalDate = timeProvider.GetLocalNow().Date.AddDays(1);
            DateTime nextLocal = DateTime.SpecifyKind(nextLocalDate, DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTimeToUtc(nextLocal, timeProvider.LocalTimeZone);
        }
    }
}
