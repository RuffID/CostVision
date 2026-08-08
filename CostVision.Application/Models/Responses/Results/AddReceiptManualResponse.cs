using CostVision.Application.Models.Dtos.Receipts;

namespace CostVision.Application.Models.Responses.Results
{
    /// <summary>
    /// Ответ на ручное добавление чека.
    /// </summary>
    public class AddReceiptManualResponse
    {
        /// <summary>
        /// Бизнес-исход операции.
        /// </summary>
        public ManualReceiptOutcome Outcome { get; set; }

        /// <summary>
        /// Пояснение бизнес-исхода.
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// Созданный чек.
        /// </summary>
        public ReceiptDto? Receipt { get; set; }
    }
}
