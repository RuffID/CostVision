using CostVision.Application.Models.Services.Receipts;
using CostVision.Infrastructure.Services.Receipts;
using Xunit;

namespace CostVision.Infrastructure.UnitTests.Services.Receipts;

public class QrParserTests
{
    [Fact]
    public void Parse_ReadsFnsQrStringWithRequiredFields()
    {
        QrParser parser = new();

        QrParsed result = parser.Parse("t=20260529T1015&s=123.45&fn=9288000100112233&i=12345&fp=67890&n=3");

        Assert.Equal(new DateTime(2026, 5, 29, 10, 15, 0), result.DateTime);
        Assert.Equal(123.45m, result.Sum);
        Assert.Equal("9288000100112233", result.FiscalDriveNumber);
        Assert.Equal("12345", result.FiscalDocumentNumber);
        Assert.Equal("67890", result.FiscalSign);
        Assert.Equal(3, result.OperationType);
    }

    [Fact]
    public void Parse_ReadsParametersInAnyOrder()
    {
        QrParser parser = new();

        QrParsed result = parser.Parse("fp=111&i=222&fn=333&s=10.50&t=20260529T101530");

        Assert.Equal(new DateTime(2026, 5, 29, 10, 15, 30), result.DateTime);
        Assert.Equal(10.50m, result.Sum);
        Assert.Equal("333", result.FiscalDriveNumber);
        Assert.Equal("222", result.FiscalDocumentNumber);
        Assert.Equal("111", result.FiscalSign);
    }

    [Fact]
    public void Parse_DecodesUrlEncodedValuesAndQueryString()
    {
        QrParser parser = new();

        QrParsed result = parser.Parse("https://qr.nalog.ru/?t=20260529T1015&s=100.00&fn=FN%2B123&i=FD%2F456&fp=FP%20789");

        Assert.Equal(new DateTime(2026, 5, 29, 10, 15, 0), result.DateTime);
        Assert.Equal(100.00m, result.Sum);
        Assert.Equal("FN+123", result.FiscalDriveNumber);
        Assert.Equal("FD/456", result.FiscalDocumentNumber);
        Assert.Equal("FP 789", result.FiscalSign);
    }

    [Fact]
    public void Parse_LeavesMissingRequiredParameterEmpty()
    {
        QrParser parser = new();

        QrParsed result = parser.Parse("t=20260529T1015&s=123.45&fn=9288000100112233&i=12345");

        Assert.Equal(string.Empty, result.FiscalSign);
        Assert.Equal(123.45m, result.Sum);
    }

    [Fact]
    public void Parse_ReturnsEmptyModelForGarbageString()
    {
        QrParser parser = new();

        QrParsed result = parser.Parse("not a qr string");

        Assert.Equal(default, result.DateTime);
        Assert.Null(result.Sum);
        Assert.Equal(string.Empty, result.FiscalDriveNumber);
        Assert.Equal(string.Empty, result.FiscalDocumentNumber);
        Assert.Equal(string.Empty, result.FiscalSign);
        Assert.Null(result.OperationType);
    }
}
