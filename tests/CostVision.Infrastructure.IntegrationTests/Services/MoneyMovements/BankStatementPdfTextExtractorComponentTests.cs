using System.Text;
using CostVision.Infrastructure.Services.MoneyMovements;
using Xunit;

namespace CostVision.Infrastructure.IntegrationTests.Services.MoneyMovements;

public class BankStatementPdfTextExtractorComponentTests
{
    [Fact]
    public async Task ExtractPagesAsync_ValidPdfWithTextLayer_ReturnsPageText()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        BankStatementPdfTextExtractor extractor = new();
        await using MemoryStream stream = new(CreatePdfWithText("T-Bank Statement 12345"));

        IReadOnlyList<string> pages = await extractor.ExtractPagesAsync(stream, ct);

        Assert.Single(pages);
        Assert.Contains("T-Bank Statement", pages[0]);
        Assert.Contains("12345", pages[0]);
    }

    [Fact]
    public async Task ExtractPagesAsync_CorruptedPdf_ThrowsPdfReadException()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        BankStatementPdfTextExtractor extractor = new();
        await using MemoryStream stream = new(Encoding.ASCII.GetBytes("not a pdf"));

        await Assert.ThrowsAnyAsync<Exception>(() => extractor.ExtractPagesAsync(stream, ct));
    }

    [Fact]
    public async Task ExtractPagesAsync_EmptyPdf_ThrowsPdfReadException()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        BankStatementPdfTextExtractor extractor = new();
        await using MemoryStream stream = new();

        await Assert.ThrowsAnyAsync<Exception>(() => extractor.ExtractPagesAsync(stream, ct));
    }

    private static byte[] CreatePdfWithText(string text)
    {
        List<string> objects =
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"
        ];

        string escapedText = EscapePdfText(text);
        string content = $"BT /F1 24 Tf 72 720 Td ({escapedText}) Tj ET";
        objects.Add($"<< /Length {content.Length} >>\nstream\n{content}\nendstream");

        StringBuilder builder = new();
        builder.Append("%PDF-1.4\n");
        List<int> offsets = [0];

        for (int index = 0; index < objects.Count; index++)
        {
            offsets.Add(builder.Length);
            builder.Append(index + 1).Append(" 0 obj\n");
            builder.Append(objects[index]).Append('\n');
            builder.Append("endobj\n");
        }

        int xrefOffset = builder.Length;
        builder.Append("xref\n");
        builder.Append("0 ").Append(objects.Count + 1).Append('\n');
        builder.Append("0000000000 65535 f \n");

        foreach (int offset in offsets.Skip(1))
            builder.Append(offset.ToString("D10")).Append(" 00000 n \n");

        builder.Append("trailer\n");
        builder.Append("<< /Size ").Append(objects.Count + 1).Append(" /Root 1 0 R >>\n");
        builder.Append("startxref\n");
        builder.Append(xrefOffset).Append('\n');
        builder.Append("%%EOF");

        return Encoding.ASCII.GetBytes(builder.ToString());
    }

    private static string EscapePdfText(string text)
    {
        return text.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
    }
}
