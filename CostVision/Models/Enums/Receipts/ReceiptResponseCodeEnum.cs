namespace CostVision.Models.Enums.Receipts
{
    /// <summary>
    /// Коды ответа сервиса получения данных чека.
    /// </summary>
    public enum ReceiptResponseCodeEnum
    {
        /// <summary>
        /// Чек некорректен.
        /// </summary>
        Incorrect = 0,

        /// <summary>
        /// Данные чека получены (успешный запрос).
        /// </summary>
        Received = 1,

        /// <summary>
        /// Данные чека пока не получены.
        /// </summary>
        Pending = 2,

        /// <summary>
        /// Превышено количество запросов.
        /// </summary>
        RateLimitExceeded = 3,

        /// <summary>
        /// Ожидание перед повторным запросом.
        /// </summary>
        WaitBeforeRetry = 4,

        /// <summary>
        /// Прочее, данные не получены.
        /// </summary>
        Other = 5
    }

}
