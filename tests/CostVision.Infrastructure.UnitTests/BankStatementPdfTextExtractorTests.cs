using System.Text;
using CostVision.Application.Abstractions.Service.MoneyMovements;
using CostVision.Infrastructure.Models.ConfigClass;
using CostVision.Infrastructure.Services.MoneyMovements;
using Microsoft.Extensions.Options;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using Xunit;

namespace CostVision.Infrastructure.UnitTests;

public class BankStatementPdfTextExtractorTests
{
    [Fact]
    public async Task ActualPdfPig_ReadsExactlyAllowedBytesAndPages_LeavesCallerStreamOpen()
    {
        // Arrange
        byte[] pdf = CreatePdf(2);
        BankStatementPdfTextExtractor extractor = CreateExtractor(pdf.Length, 2);
        using MemoryStream stream = new(pdf);

        // Act
        IReadOnlyList<string> pages = await extractor.ExtractPagesAsync(stream, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, pages.Count);
        Assert.Contains("Statement 1", pages[0]);
        Assert.Contains("Statement 2", pages[1]);
        Assert.True(stream.CanRead);
    }

    [Fact]
    public async Task SeekableOversize_IsRejectedBeforeReadingOrOpeningPdf()
    {
        // Arrange
        using ProbeStream stream = new(new byte[101], seekable: true);
        BankStatementPdfTextExtractor extractor = CreateExtractor(100, 2);

        // Act
        BankStatementPdfException error = await Assert.ThrowsAsync<BankStatementPdfException>(
            () => extractor.ExtractPagesAsync(stream, TestContext.Current.CancellationToken));

        // Assert
        Assert.Contains("Размер PDF", error.Message);
        Assert.Equal(0, stream.BytesRead);
        Assert.True(stream.CanRead);
    }

    [Fact]
    public async Task NonSeekableOversize_ReadsOnlyLimitPlusOne_WithoutParsing()
    {
        // Arrange
        using ProbeStream stream = new(new byte[1000], seekable: false);
        BankStatementPdfTextExtractor extractor = CreateExtractor(100, 2);

        // Act
        BankStatementPdfException error = await Assert.ThrowsAsync<BankStatementPdfException>(
            () => extractor.ExtractPagesAsync(stream, TestContext.Current.CancellationToken));

        // Assert
        Assert.Contains("Размер PDF", error.Message);
        Assert.Equal(101, stream.BytesRead);
        Assert.True(stream.CanRead);
    }

    [Fact]
    public async Task ActualPdfPig_RejectsPageLimitInsteadOfTruncating()
    {
        // Arrange
        using MemoryStream stream = new(CreatePdf(3));
        BankStatementPdfTextExtractor extractor = CreateExtractor(100_000, 2);

        // Act
        BankStatementPdfException error = await Assert.ThrowsAsync<BankStatementPdfException>(
            () => extractor.ExtractPagesAsync(stream, TestContext.Current.CancellationToken));

        // Assert
        Assert.Contains("Количество страниц", error.Message);
        Assert.True(stream.CanRead);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a pdf secret")]
    [InlineData("%PDF-1.4\ntruncated secret")]
    public async Task ActualPdfPig_MalformedDocumentHasSafeExpectedFailure(string value)
    {
        // Arrange
        using MemoryStream stream = new(Encoding.ASCII.GetBytes(value));
        BankStatementPdfTextExtractor extractor = CreateExtractor(1000, 2);

        // Act
        BankStatementPdfException error = await Assert.ThrowsAsync<BankStatementPdfException>(
            () => extractor.ExtractPagesAsync(stream, TestContext.Current.CancellationToken));

        // Assert
        Assert.Contains("PDF", error.Message);
        Assert.DoesNotContain("secret", error.Message);
        Assert.Null(error.InnerException);
        Assert.True(stream.CanRead);
    }

    [Fact]
    public async Task CancellationDuringBufferRead_IsNotClassifiedAsMalformedPdf()
    {
        // Arrange
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using ProbeStream stream = new(CreatePdf(1), seekable: false, afterRead: cancellation.Cancel);
        BankStatementPdfTextExtractor extractor = CreateExtractor(100_000, 2);

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => extractor.ExtractPagesAsync(stream, cancellation.Token));

        // Assert
        Assert.True(cancellation.IsCancellationRequested);
        Assert.True(stream.CanRead);
    }

    [Fact]
    public async Task UnexpectedIoFailure_IsNotClassifiedAsMalformedPdf()
    {
        // Arrange
        IOException expected = new("test-only stream failure");
        using ProbeStream stream = new([1], seekable: false, error: expected);
        BankStatementPdfTextExtractor extractor = CreateExtractor(1000, 2);

        // Act
        IOException actual = await Assert.ThrowsAsync<IOException>(() => extractor.ExtractPagesAsync(stream, TestContext.Current.CancellationToken));

        // Assert
        Assert.Same(expected, actual);
        Assert.True(stream.CanRead);
    }

    private static BankStatementPdfTextExtractor CreateExtractor(int bytes, int pages)
    {
        return new BankStatementPdfTextExtractor(Options.Create(new BankStatementPdfSettings { MaxBytes = bytes, MaxPages = pages }));
    }

    private static byte[] CreatePdf(int pages)
    {
        using PdfDocumentBuilder builder = new();
        PdfDocumentBuilder.AddedFont font = builder.AddStandard14Font(Standard14Font.Helvetica);
        for (int index = 1; index <= pages; index++)
        {
            builder.AddPage(200, 200).AddText($"Statement {index}", 12, new PdfPoint(10, 150), font);
        }

        return builder.Build();
    }

    private sealed class ProbeStream(byte[] data, bool seekable, Action? afterRead = null, Exception? error = null) : MemoryStream(data)
    {
        public int BytesRead { get; private set; }
        public override bool CanSeek => seekable;

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (error != null)
            {
                throw error;
            }

            int read = base.Read(buffer.Span);
            BytesRead += read;
            afterRead?.Invoke();

            return ValueTask.FromResult(read);
        }
    }
}
