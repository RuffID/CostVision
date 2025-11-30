using CostVision.Models.Services.Receipts;
using System.Globalization;

namespace CostVision.Services.Receipts
{
    public class QrParser
    {
        public QrParsed Parse(string qrString)
        {
            QrParsed model = new();

            if (string.IsNullOrWhiteSpace(qrString))
                return model;

            // Разделяет строку на параметры по символу '&'
            string[] parts = qrString.Split('&', StringSplitOptions.RemoveEmptyEntries);

            foreach (string part in parts)
            {
                // Разделяет параметр на ключ и значение
                string[] kv = part.Split('=', 2);

                // Проверяет корректность пары ключ-значение
                if (kv.Length != 2)
                    continue;

                string key = kv[0];
                string value = kv[1];

                switch (key)
                {
                    case "t":
                        // Выполняет анализ строки даты и времени; поддерживает форматы с секундами и без
                        string[] formats = { "yyyyMMdd'T'HHmm", "yyyyMMdd'T'HHmmss" };

                        // Проверяет количество символов и пытается распарсить дату
                        if (value.Length >= 13 && DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt))
                            model.DateTime = dt;
                        break;
                    case "s":
                        // Выполняет анализ строки суммы чека
                        if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal sum))
                            model.Sum = sum;
                        break;
                    case "fn":
                        // Сохраняет номер фискального накопителя
                        model.FiscalDriveNumber = value;
                        break;
                    case "i":
                        // Сохраняет номер фискального документа
                        model.FiscalDocumentNumber = value;
                        break;
                    case "fp":
                        // Сохраняет фискальный признак документа
                        model.FiscalSign = value;
                        break;
                    case "n":
                        // Выполняет анализ строки типа операции
                        if (int.TryParse(value, out int op))
                            model.OperationType = op;
                        break;
                    default:
                        // Игнорирует неизвестные параметры
                        break;
                }
            }

            return model;
        }
    }
}
