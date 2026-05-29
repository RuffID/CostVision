using CostVision.Application.Abstractions.Service.Receipts;
using CostVision.Application.Models.Services.Receipts;
using System.Globalization;
using System.Net;

namespace CostVision.Infrastructure.Services.Receipts
{
    public class QrParser : IQrParser
    {
        public QrParsed Parse(string qrString)
        {
            QrParsed model = new();

            if (string.IsNullOrWhiteSpace(qrString))
                return model;

            string queryString = qrString;
            int queryStartIndex = qrString.IndexOf('?');
            if (queryStartIndex >= 0)
                queryString = qrString[(queryStartIndex + 1)..];

            string[] parts = queryString.Split('&', StringSplitOptions.RemoveEmptyEntries);

            foreach (string part in parts)
            {
                string[] kv = part.Split('=', 2);
                if (kv.Length != 2)
                    continue;

                string key = WebUtility.UrlDecode(kv[0]);
                string value = WebUtility.UrlDecode(kv[1]);

                switch (key)
                {
                    case "t":
                        string[] formats = { "yyyyMMdd'T'HHmm", "yyyyMMdd'T'HHmmss" };
                        if (value.Length >= 13 && DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt))
                            model.DateTime = dt;
                        break;
                    case "s":
                        if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal sum))
                            model.Sum = sum;
                        break;
                    case "fn":
                        model.FiscalDriveNumber = value;
                        break;
                    case "i":
                        model.FiscalDocumentNumber = value;
                        break;
                    case "fp":
                        model.FiscalSign = value;
                        break;
                    case "n":
                        if (int.TryParse(value, out int op))
                            model.OperationType = op;
                        break;
                }
            }

            return model;
        }
    }
}
