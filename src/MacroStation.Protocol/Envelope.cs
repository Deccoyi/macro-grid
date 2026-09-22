using System.Text.Json;
using System.Text.Json.Nodes;

namespace MacroStation.Protocol;

/// <summary>
/// Every WebSocket frame is a JSON envelope: { "type": "widget.down", "data": { ... } }.
/// The client-side TypeScript types in client/packages/renderer mirror these shapes.
/// </summary>
public sealed record Envelope(string Type, JsonNode? Data)
{
    public T? DataAs<T>() => Data is null ? default : Data.Deserialize<T>(ProtocolJson.Options);

    public static Envelope Create<T>(string type, T data) =>
        new(type, JsonSerializer.SerializeToNode(data, ProtocolJson.Options));

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
