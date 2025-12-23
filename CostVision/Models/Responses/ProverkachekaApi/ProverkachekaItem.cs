using CostVision.Models.Enums.Receipts;

namespace CostVision.Models.Responses.ProverkachekaApi
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

        // Тип оплаты
        public PaymentType PaymentType { get; set; }

        // Тип товара
        public ProductType ProductType { get; set; }

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
