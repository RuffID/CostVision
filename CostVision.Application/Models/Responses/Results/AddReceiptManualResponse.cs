using CostVision.Application.Models.Dtos.Receipts;

namespace CostVision.Application.Models.Responses.Results
{
    /// <summary>
    /// Ответ на ручное добавление чека.
    /// </summary>
    public class AddReceiptManualResponse
    {
        /// <summary>
        /// Признак успешного создания чека.
        /// </summary>
        public bool IsCreated { get; set; }

        /// <summary>
        /// Сообщение об ошибке или пояснение результата.
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// Созданный чек.
        /// </summary>
        public ReceiptDto? Receipt { get; set; }
    }
}
