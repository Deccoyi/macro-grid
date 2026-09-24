using System.Text.Json.Nodes;

namespace MacroGrid.Core.Json;

public static class JsonNodeExtensions
{
    /// <summary>
    /// Reads a JSON number as a double regardless of the CLR type it was built with.
    /// A plain <c>node.GetValue&lt;double&gt;()</c> throws for a node created in code as <c>JsonValue.Create(40)</c>
    /// (an int-backed node), since JsonValue only converts numeric types when the node came from parsed JSON text.
    /// </summary>
    public static double? AsDouble(this JsonNode? node)
    {
        if (node is not JsonValue value) return null;
        if (value.TryGetValue<double>(out var d)) return d;
        if (value.TryGetValue<int>(out var i)) return i;
        if (value.TryGetValue<long>(out var l)) return l;
        if (value.TryGetValue<float>(out var f)) return f;
        return null;
    }
}
