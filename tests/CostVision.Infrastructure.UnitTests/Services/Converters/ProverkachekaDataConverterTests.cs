using System.Text.Json;
using CostVision.Infrastructure.Models.Responses.ProverkachekaApi;
using Xunit;

namespace CostVision.Infrastructure.UnitTests.Services.Converters;

public class ProverkachekaDataConverterTests
{
    [Fact]
    public void Deserialize_ReadsObjectData()
    {
        string json = """
        {
          "code": 1,
          "data": {
            "json": {
              "fiscalDriveNumber": "fn",
              "fiscalDocumentNumber": 123,
              "fiscalSign": 456,
              "totalSum": 789
            },
            "html": "<html></html>"
          }
        }
        """;

        ProverkachekaResponse? result = JsonSerializer.Deserialize<ProverkachekaResponse>(json, CreateOptions());

        Assert.NotNull(result);
        Assert.Equal("fn", result.Data?.Json?.FiscalDriveNumber);
        Assert.Equal(123, result.Data?.Json?.FiscalDocumentNumber);
        Assert.Equal(456, result.Data?.Json?.FiscalSign);
        Assert.Equal(789, result.Data?.Json?.TotalSum);
        Assert.Equal("<html></html>", result.Data?.Html);
    }

    [Fact]
    public void Deserialize_ReadsStringDataAsError()
    {
        string json = """
        {
          "code": 2,
          "data": "not found"
        }
        """;

        ProverkachekaResponse? result = JsonSerializer.Deserialize<ProverkachekaResponse>(json, CreateOptions());

        Assert.NotNull(result);
        Assert.Equal("not found", result.Data?.Error);
        Assert.True(result.Data?.HasError);
        Assert.Null(result.Data?.Json);
    }

    [Fact]
    public void Deserialize_ReadsNullData()
    {
        string json = """
        {
          "code": 2,
          "data": null
        }
        """;

        ProverkachekaResponse? result = JsonSerializer.Deserialize<ProverkachekaResponse>(json, CreateOptions());

        Assert.NotNull(result);
        Assert.Null(result.Data);
    }

    [Fact]
    public void Deserialize_ReadsEmptyObject()
    {
        string json = """
        {
          "code": 2,
          "data": {}
        }
        """;

        ProverkachekaResponse? result = JsonSerializer.Deserialize<ProverkachekaResponse>(json, CreateOptions());

        Assert.NotNull(result);
        Assert.NotNull(result.Data);
        Assert.Null(result.Data.Json);
        Assert.Null(result.Data.Html);
        Assert.Null(result.Data.Error);
    }

    [Fact]
    public void Deserialize_ThrowsForInvalidDataToken()
    {
        string json = """
        {
          "code": 2,
          "data": 123
        }
        """;

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ProverkachekaResponse>(json, CreateOptions()));
    }

    private static JsonSerializerOptions CreateOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }
}
