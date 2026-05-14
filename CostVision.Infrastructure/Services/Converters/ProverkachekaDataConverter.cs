using CostVision.Infrastructure.Models.Responses.ProverkachekaApi;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CostVision.Infrastructure.Services.Converters
{
    public class ProverkachekaDataConverter : JsonConverter<ProverkachekaData>
    {
        public override ProverkachekaData? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return null;

            if (reader.TokenType == JsonTokenType.String)
            {
                return new ProverkachekaData
                {
                    Error = reader.GetString()
                };
            }

            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException($"Unexpected token type for Proverkacheka data: {reader.TokenType}.");

            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            JsonElement root = document.RootElement;

            ProverkachekaData result = new();
            if (root.TryGetProperty("json", out JsonElement jsonElement) && jsonElement.ValueKind != JsonValueKind.Null)
                result.Json = jsonElement.Deserialize<ProverkachekaJson>(options);

            if (root.TryGetProperty("html", out JsonElement htmlElement) && htmlElement.ValueKind != JsonValueKind.Null)
                result.Html = htmlElement.GetString();

            return result;
        }

        public override void Write(Utf8JsonWriter writer, ProverkachekaData value, JsonSerializerOptions options)
        {
            if (value.Json == null && value.Html == null && !string.IsNullOrWhiteSpace(value.Error))
            {
                writer.WriteStringValue(value.Error);
                return;
            }

            writer.WriteStartObject();

            if (value.Json != null)
            {
                writer.WritePropertyName("json");
                JsonSerializer.Serialize(writer, value.Json, options);
            }

            if (value.Html != null)
                writer.WriteString("html", value.Html);

            writer.WriteEndObject();
        }
    }
}
