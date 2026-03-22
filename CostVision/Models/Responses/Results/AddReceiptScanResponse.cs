using CostVision.Models.Services.Receipts;

namespace CostVision.Models.Responses.Results
{
    /// <summary>
    /// Ответ на добавление чеков из результатов сканирования.
    /// </summary>
    public class AddReceiptScanResponse
    {
        /// <summary>
        /// Общее количество обработанных результатов.
        /// </summary>
        public int ScannedCount { get; set; }

        /// <summary>
        /// Количество чеков, добавленных в базу данных.
        /// </summary>
        public int AddedToDbCount { get; set; }

        /// <summary>
        /// Количество результатов с ошибками.
        /// </summary>
        public int ErrorCount { get; set; }

        /// <summary>
        /// Подробные результаты обработки каждого QR-кода.
        /// </summary>
        public List<QrScanResult> Results { get; set; } = new();
    }
}
