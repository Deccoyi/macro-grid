using System.Text.Json;

namespace MacroGrid.Core.Plugins.Distribution;

/// <summary>One line of the revoke list: a plugin (every version when <see cref="Versions"/> is null) and the English reason shown to the person.</summary>
public sealed record RevokedPlugin(string Id, IReadOnlyList<string>? Versions, string Reason);

/// <summary>
/// The official revoke list (payload of the signed <c>revoked</c> file). Only a list that was verified with the official key is ever parsed
/// here. An entry that is not understood is skipped so one odd entry cannot hide the others; a format other than 1 is refused as a whole.
/// New fields may only be added, never changed, so the format stays 1.
/// </summary>
public sealed class PluginRevocationList
{
    public const int MaxEntries = 500;
    public const int MaxReasonLength = 200;
    public const string DefaultReason = "This version is not safe to use.";

    private readonly IReadOnlyList<RevokedPlugin> _entries;

    private PluginRevocationList(IReadOnlyList<RevokedPlugin> entries) => _entries = entries;

    public IReadOnlyList<RevokedPlugin> Entries => _entries;

    public static readonly PluginRevocationList Empty = new([]);

    /// <summary>Null when the payload is not a revoke list this server understands.</summary>
    public static PluginRevocationList? Parse(byte[] payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            if (!root.TryGetProperty("formatVersion", out var fv) || !fv.TryGetInt32(out var format) || format != 1) return null;

            var entries = new List<RevokedPlugin>();
            if (root.TryGetProperty("plugins", out var plugins) && plugins.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in plugins.EnumerateArray())
                {
                    if (entries.Count >= MaxEntries) break;
                    if (ParseEntry(element) is { } entry) entries.Add(entry);
                }
            }
            return new PluginRevocationList(entries);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static RevokedPlugin? ParseEntry(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        if (!element.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(id.GetString())) return null;

        List<string>? versions = null;
        if (element.TryGetProperty("versions", out var list))
        {
            if (list.ValueKind != JsonValueKind.Array) return null;
            versions = [.. list.EnumerateArray().Where(v => v.ValueKind == JsonValueKind.String).Select(v => v.GetString()!)];
            if (versions.Count == 0) return null; // an empty list must not turn into "every version"
        }

        var reason = element.TryGetProperty("reason", out var r) && r.ValueKind == JsonValueKind.String
            ? CatalogText.Clean(r.GetString(), MaxReasonLength)
            : null;
        return new RevokedPlugin(id.GetString()!.Trim(), versions, reason ?? DefaultReason);
    }

    /// <summary>The entry that covers this plugin version, or null. The id is compared ignoring case, versions as exact text.</summary>
    public RevokedPlugin? Find(string id, string version) =>
        _entries.FirstOrDefault(e => e.Id.Equals(id, StringComparison.OrdinalIgnoreCase) && (e.Versions is null || e.Versions.Contains(version, StringComparer.Ordinal)));
}
