using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace MacroGrid.Core.Sessions;

/// <summary>
/// Holds the large <c>data:</c> values (icons, images) that were pulled out of layouts sent to clients, keyed by
/// a content hash, so each distinct value crosses the wire once per client instead of once per widget per
/// layout. A layout carries the short reference (<c>asset:&lt;hash&gt;</c>); the client asks for the data of
/// references it has not cached yet. Because the key is the content hash, a cached asset never goes stale.
/// </summary>
public sealed class AssetStore
{
    public const string RefPrefix = "asset:";

    /// <summary>Values shorter than this stay inline — a reference would not be smaller.</summary>
    private const int MinExternalizedLength = 200;
    private const int MaxEntries = 2000;

    private readonly Lock _lock = new();
    private readonly Dictionary<string, LinkedListNode<(string Hash, string Data)>> _byHash = [];
    private readonly LinkedList<(string Hash, string Data)> _lru = new();

    public string? Get(string hash)
    {
        lock (_lock)
            return _byHash.TryGetValue(hash, out var node) ? node.Value.Data : null;
    }

    /// <summary>Stores a value and returns its reference. Every send re-touches the assets it uses, so only
    /// values no layout references any more fall off the end when the store is full.</summary>
    public string Put(string data)
    {
        var hash = Hash(data);
        lock (_lock)
        {
            if (_byHash.TryGetValue(hash, out var existing))
            {
                _lru.Remove(existing);
                _lru.AddFirst(existing);
            }
            else
            {
                _byHash[hash] = _lru.AddFirst((hash, data));
                while (_byHash.Count > MaxEntries && _lru.Last is { } oldest)
                {
                    _byHash.Remove(oldest.Value.Hash);
                    _lru.RemoveLast();
                }
            }
        }
        return RefPrefix + hash;
    }

    /// <summary>Replaces, in place, every large <c>data:</c> string anywhere in <paramref name="node"/> with an
    /// asset reference. Returns the node (a fresh value if the root itself was a string).</summary>
    public JsonNode? Externalize(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var key in obj.Select(kv => kv.Key).ToList())
                {
                    // Re-assigning a child to its own parent throws, so only write back a replaced value.
                    var child = obj[key];
                    var replaced = Externalize(child);
                    if (!ReferenceEquals(child, replaced)) obj[key] = replaced;
                }
                return obj;
            case JsonArray array:
                for (var i = 0; i < array.Count; i++)
                {
                    var child = array[i];
                    var replaced = Externalize(child);
                    if (!ReferenceEquals(child, replaced)) array[i] = replaced;
                }
                return array;
            case JsonValue value when value.TryGetValue<string>(out var text) && IsExternalizable(text):
                return JsonValue.Create(Put(text));
            default:
                return node;
        }
    }

    /// <summary>The same replacement for a single value (a style pushed in a <c>widget.state</c>).</summary>
    public string ExternalizeValue(string value) => IsExternalizable(value) ? Put(value) : value;

    private static bool IsExternalizable(string text) =>
        text.Length >= MinExternalizedLength && text.StartsWith("data:", StringComparison.Ordinal);

    private static string Hash(string data) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(data)))[..24];
}
