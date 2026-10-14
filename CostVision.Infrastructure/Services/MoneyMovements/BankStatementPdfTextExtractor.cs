using CostVision.Application.Abstractions.Service.MoneyMovements;
using CostVision.Infrastructure.Models.ConfigClass;
using Microsoft.Extensions.Options;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig.Exceptions;

namespace CostVision.Infrastructure.Services.MoneyMovements
{
    public class BankStatementPdfTextExtractor(IOptions<BankStatementPdfSettings> settings) : IBankStatementPdfTextExtractor
    {
        public async Task<IReadOnlyList<string>> ExtractPagesAsync(Stream pdfStream, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            BankStatementPdfSettings options = settings.Value;
            if (pdfStream.CanSeek && pdfStream.Length - pdfStream.Position > options.MaxBytes)
            {
                throw new BankStatementPdfException("Размер PDF превышает допустимый лимит выписки.");
            }

            using MemoryStream buffer = new();
            byte[] chunk = new byte[81920];
            while (true)
            {
                int remaining = (int)Math.Min(chunk.Length, (long)options.MaxBytes - buffer.Length + 1);
                int read = await pdfStream.ReadAsync(chunk.AsMemory(0, remaining), ct);
                if (read == 0)
                {
                    break;
                }

                if (buffer.Length + read > options.MaxBytes)
                {
                    throw new BankStatementPdfException("Размер PDF превышает допустимый лимит выписки.");
                }

                buffer.Write(chunk, 0, read);
            }

            ct.ThrowIfCancellationRequested();
            if (buffer.Length < 9)
            {
                throw new BankStatementPdfException("PDF повреждён или имеет неподдерживаемую структуру.");
            }

            buffer.Position = 0;
            try
            {
                using PdfDocument document = PdfDocument.Open(buffer);
                if (document.NumberOfPages > options.MaxPages)
                {
                    throw new BankStatementPdfException("Количество страниц PDF превышает допустимый лимит выписки.");
                }

                List<string> pages = new();
                for (int number = 1; number <= document.NumberOfPages; number++)
                {
                    ct.ThrowIfCancellationRequested();
                    Page page = document.GetPage(number);
                    pages.Add(ContentOrderTextExtractor.GetText(page));
                }

                ct.ThrowIfCancellationRequested();

                return pages;
            }
            catch (PdfDocumentFormatException)
            {
                throw new BankStatementPdfException("PDF повреждён или имеет неподдерживаемую структуру.");
            }
            catch (PdfDocumentEncryptedException)
            {
                throw new BankStatementPdfException("PDF защищён паролем. Загрузите выписку без защиты.");
            }
        }
    }
}
