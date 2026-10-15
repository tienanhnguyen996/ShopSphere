using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShopSphere.BuildingBlocks.Messaging.Serialization;

public static class ShopSphereJsonDefaults
{
    private static readonly JsonSerializerOptions _options = CreateDefaultOptions();

    public static JsonSerializerOptions DefaultOptions => _options;

    public static JsonSerializerOptions CreateDefaultOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = false
        };

        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new UtcDateTimeJsonConverter());
        return options;
    }

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, _options);

    public static T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, _options);
}

public sealed class UtcDateTimeJsonConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var date = reader.GetDateTime();
        return DateTime.SpecifyKind(date, DateTimeKind.Utc);
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        var utc = value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
        writer.WriteStringValue(utc.ToString("yyyy-MM-ddTHH:mm:ss.FFFFFFFZ"));
    }
}
