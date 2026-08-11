using CostVision.Domain.Models.Enums.Receipts;
using System.Text.Json.Serialization;

namespace CostVision.Infrastructure.Models.Responses.ProverkachekaApi
{
    public class ProverkachekaItem
    {
        // НДС-ставка
        public int Nds { get; set; }

        // Сумма позиции в копейках
        public int Sum { get; set; }

        // Наименование товара
        public string Name { get; set; } = string.Empty;
        
        // Код товара
        public ProverkachekaProductCode? ProductCode { get; set; }

        // Цена в копейках
        public int Price { get; set; }

        // Количество товара
        public decimal Quantity { get; set; }

        // Признак способа расчёта (тег 1214 ФФД)
        [JsonPropertyName("paymentType")]
        public int PaymentTypeCode { get; set; }

        // Признак предмета расчёта (тег 1212 ФФД)
        [JsonPropertyName("productType")]
        public int ProductTypeCode { get; set; }

        // Мера количества
        public QuantityMeasureType ItemsQuantityMeasure { get; set; }

        // Маркировка
        public ProverkachekaProductCodeNew? ProductCodeNew { get; set; }

        // Режим обработки кода маркировки
        public int? LabelCodeProcesMode { get; set; }

        // Отраслевые сведения
        public List<ProverkachekaIndustryDetail>? ItemsIndustryDetails { get; set; }

        // Результат проверки инфо по продукции
        public int? CheckingProdInformationResult { get; set; }
    } 
}
