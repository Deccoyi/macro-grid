using System.Text.Json;
using System.Text.Json.Nodes;

namespace MacroGrid.Protocol;

/// <summary>
/// Every WebSocket frame is a JSON envelope: { "type": "widget.down", "data": { ... } }.
/// The client-side TypeScript types in client/packages/renderer mirror these shapes.
/// </summary>
public sealed record Envelope(string Type, JsonNode? Data)
{
    public T? DataAs<T>() => Data is null ? default : Data.Deserialize<T>(ProtocolJson.Options);

    public static Envelope Create<T>(string type, T data) =>
        new(type, JsonSerializer.SerializeToNode(data, ProtocolJson.Options));

    /// <summary>The frame <c>Create(type, data).ToJson()</c> would produce, as UTF-8, without the intermediate <see cref="JsonNode"/> and string.</summary>
    public static byte[] ToUtf8<T>(string type, T data)
    {
        var buffer = new System.Buffers.ArrayBufferWriter<byte>(256);
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("type", type);
            if (data is not null)
            {
                writer.WritePropertyName("data");
                JsonSerializer.Serialize(writer, data, ProtocolJson.Options);
            }
            writer.WriteEndObject();
        }
        return buffer.WrittenSpan.ToArray();
    }

    public string ToJson() => JsonSerializer.Serialize(this, ProtocolJson.Options);

    public static Envelope? Parse(string json) =>
        JsonSerializer.Deserialize<Envelope>(json, ProtocolJson.Options);
}

public static class ProtocolJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };
}
