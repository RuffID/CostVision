using CostVision.Application.Models.Services.Receipts;

namespace CostVision.Application.Abstractions.Service.Receipts
{
    public interface IQrParser
    {
        QrParsed Parse(string qrString);
    }
}
