using CostVision.Application.Abstractions.Service.MoneyMovements;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace CostVision.Infrastructure.Services.MoneyMovements
{
    public class BankStatementPdfTextExtractor : IBankStatementPdfTextExtractor
    {
        public Task<IReadOnlyList<string>> ExtractPagesAsync(Stream pdfStream, CancellationToken ct)
        {
            List<string> pages = new();

            using PdfDocument document = PdfDocument.Open(pdfStream);
            foreach (Page page in document.GetPages())
            {
                ct.ThrowIfCancellationRequested();
                pages.Add(ContentOrderTextExtractor.GetText(page));
            }

            return Task.FromResult<IReadOnlyList<string>>(pages);
        }
    }
}
