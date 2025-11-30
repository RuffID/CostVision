using CostVision.Models.Responses.ProverkachekaApi;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CostVision.Services.Converters
{
    public class ProverkachekaDataConverter : JsonConverter<ProverkachekaData>
    {
        public override ProverkachekaData? ReadJson(JsonReader reader, Type objectType, ProverkachekaData? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            // data = "Не вышло время ожидания..."
            if (reader.TokenType == JsonToken.String)
            {
                string errorText = (string)reader.Value!;
                return new ProverkachekaData
                {
                    Error = errorText
                };
            }

            // data = { "json": {...}, "html": "..." }
            if (reader.TokenType == JsonToken.StartObject)
            {
                JObject obj = JObject.Load(reader);

                ProverkachekaData result = new ();
                serializer.Populate(obj.CreateReader(), result);
                return result;
            }

            // любой другой мусор
            JToken token = JToken.Load(reader);
            return new ProverkachekaData
            {
                Error = token.ToString()
            };
        }

        public override void WriteJson(JsonWriter writer, ProverkachekaData? value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            // если это просто ошибка — пишем строкой
            if (value.Json == null && value.Html == null && !string.IsNullOrWhiteSpace(value.Error))
            {
                writer.WriteValue(value.Error);
                return;
            }

            // иначе сериализуем как объект { json: ..., html: ... }
            JObject obj = new();

            if (value.Json != null)
                obj["json"] = JToken.FromObject(value.Json, serializer);

            if (value.Html != null)
                obj["html"] = value.Html;

            obj.WriteTo(writer);
        }
    }
}
