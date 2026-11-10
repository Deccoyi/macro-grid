using System.Text;
using System.Text.Json.Nodes;
using MacroGrid.Protocol;

namespace MacroGrid.Tests;

/// <summary>The direct UTF-8 writer must put exactly the bytes on the wire that the node-and-string path did.</summary>
public sealed class EnvelopeWireTests
{
    private static void SameBytes<T>(string type, T data) =>
        Assert.Equal(Encoding.UTF8.GetBytes(Envelope.Create(type, data).ToJson()), Envelope.ToUtf8(type, data));

    [Fact]
    public void Typed_messages_serialize_to_the_same_bytes()
    {
        SameBytes(MessageTypes.WidgetState, new WidgetStateMessage("w", Text: "héllo \"q\" <b> ç😀", Value: 0.1 + 0.2, Active: true,
            Style: new Dictionary<string, string> { ["Background"] = "#ff0000", ["icon"] = "a/b" }, Unavailable: ["x", "y"]));
        SameBytes(MessageTypes.WidgetState, new WidgetStateMessage("w", Style: []));
        SameBytes(MessageTypes.WidgetState, new WidgetStateMessage("w"));
        SameBytes(MessageTypes.Error, new ErrorMessage("bad", "Invalid \n text", 5, null));
    }

    [Fact]
    public void Free_form_and_missing_data_serialize_to_the_same_bytes()
    {
        SameBytes("x.y", new JsonObject { ["a"] = 1, ["b"] = new JsonArray(1.5, "s", null), ["c"] = null, ["d"] = new JsonObject { ["Nested"] = true } });
        SameBytes<object?>("x.y", null);
        SameBytes("x.y", new Dictionary<string, object?> { ["Key"] = 3L, ["Other"] = null });
        SameBytes("x.y", "text");
        SameBytes("x.y", 42);
    }
}
