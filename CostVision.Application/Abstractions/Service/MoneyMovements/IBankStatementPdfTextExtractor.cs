namespace CostVision.Application.Abstractions.Service.MoneyMovements
{
    public interface IBankStatementPdfTextExtractor
    {
        Task<IReadOnlyList<string>> ExtractPagesAsync(Stream pdfStream, CancellationToken ct);
    }
}
