namespace MacroGrid.Core.Plugins.Distribution;

/// <summary>
/// Fetches a catalog plugin's icon for the editor, which never loads a remote image itself. The address is built from the newest version's own release
/// (the icon is one of that release's assets, named in the index entry), so it always points at the repository the index came from. Only a real PNG or an
/// SVG is accepted, at most 100 KB; the result is kept in memory. Any failure gives null and the editor shows the plugin's initial instead.
/// </summary>
public sealed class PluginCatalogIcons(PluginPackageDownloader downloader)
{
    public const int MaxBytes = 100 * 1024;
    private const int MaxCached = 64;

    private readonly Lock _lock = new();
    private readonly Dictionary<Uri, (byte[] Data, string ContentType)?> _cache = [];

    public async Task<(byte[] Data, string ContentType)?> GetAsync(string owner, string repo, PluginCatalogEntry entry, CancellationToken cancellationToken)
    {
        if (entry.Icon is null) return null;
        var latest = entry.Versions.OrderByDescending(v => v.Version, Comparer<string>.Create(SemVer.CompareVersionStrings)).FirstOrDefault();
        if (latest is null || !Uri.TryCreate(latest.Url, UriKind.Absolute, out var package) || !PluginSourceUrls.BelongsToRepo(package, owner, repo)) return null;

        var url = new Uri(package, entry.Icon);
        if (!PluginSourceUrls.BelongsToRepo(url, owner, repo)) return null;
        lock (_lock)
        {
            if (_cache.TryGetValue(url, out var cached)) return cached;
        }

        (byte[], string)? result = null;
        try
        {
            var bytes = await downloader.FetchBytesAsync(url, MaxBytes, cancellationToken);
            result = Classify(bytes);
        }
        catch (Exception ex) when (ex is PluginDownloadException or OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            // Not cached: a network failure may pass.
            return null;
        }

        lock (_lock)
        {
            if (_cache.Count >= MaxCached) _cache.Clear();
            _cache[url] = result;
        }
        return result;
    }

    private static (byte[], string)? Classify(byte[] bytes)
    {
        if (bytes.Length > 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47) return (bytes, "image/png");
        var start = System.Text.Encoding.UTF8.GetString(bytes, 0, Math.Min(bytes.Length, 512)).TrimStart('\uFEFF', ' ', '\r', '\n', '\t');
        if (start.StartsWith("<svg", StringComparison.OrdinalIgnoreCase) || start.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase)) return (bytes, "image/svg+xml");
        return null;
    }
}
