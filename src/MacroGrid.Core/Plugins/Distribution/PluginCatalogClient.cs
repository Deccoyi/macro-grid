using System.Text.Json;

namespace MacroGrid.Core.Plugins.Distribution;

/// <summary>Why an index or manifest could not be fetched or trusted. <see cref="Code"/> is stable so the editor
/// can show a translated message.</summary>
public sealed class PluginCatalogException(string code, string message, Exception? inner = null) : Exception(message, inner)
{
    /// <summary>No network, a non-2xx status, or the response did not look like JSON.</summary>
    public const string Fetch = "fetch";

    /// <summary>The response is larger than the size cap.</summary>
    public const string TooLarge = "too-large";

    /// <summary>The JSON parsed but did not follow the schema (see website/reference/source-index.md), or an
    /// entry's own rules were broken (a version pointing outside its own repository, a missing sha256, ...).</summary>
    public const string Invalid = "invalid";

    public string Code { get; } = code;
}

/// <summary>
/// Fetches and validates a <c>macrogrid-index.json</c> or a single-plugin <c>plugin.json</c> from a fixed
/// <c>raw.githubusercontent.com</c> URL — never the GitHub API. The <see cref="HttpClient"/> handed in is used only
/// when the caller asks (Discover opened, or an install started); nothing here runs on its own.
/// </summary>
public sealed class PluginCatalogClient(HttpClient http)
{
    private const long MaxIndexBytes = 1024 * 1024;
    private const long MaxManifestBytes = 64 * 1024;

    public async Task<PluginCatalogIndex> FetchIndexAsync(string owner, string repo, CancellationToken cancellationToken)
    {
        var json = await FetchTextAsync(PluginSourceUrls.IndexUrl(owner, repo), MaxIndexBytes, cancellationToken);
        return Parse(json, owner, repo);
    }

    /// <summary>Method 4 step 1: reads a single-plugin repository's root <c>plugin.json</c> directly (no index).</summary>
    public async Task<PluginCatalogVersion> FetchSinglePluginManifestAsync(string owner, string repo, CancellationToken cancellationToken)
    {
        var json = await FetchTextAsync(PluginSourceUrls.ManifestUrl(owner, repo), MaxManifestBytes, cancellationToken);
        JsonElement root;
        try
        {
            using var document = JsonDocument.Parse(json);
            root = document.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            throw new PluginCatalogException(PluginCatalogException.Invalid, "plugin.json is not valid JSON.", ex);
        }

        var version = GetRequiredString(root, "version", "plugin.json");
        return new PluginCatalogVersion(
            version,
            GetRequiredString(root, "sdkVersion", "plugin.json"),
            GetRequiredString(root, "minServerVersion", "plugin.json"),
            Url: "", // resolved by the caller from the repo + version (see source-index.md "Single-plugin repository")
            Sha256: "",
            Size: 0,
            Permissions: root.TryGetProperty("permissions", out var perms) && perms.ValueKind == JsonValueKind.Array
                ? [.. perms.EnumerateArray().Where(p => p.ValueKind == JsonValueKind.String).Select(p => p.GetString()!)]
                : null,
            Signature: null);
    }

    /// <summary>True when the root has <c>macrogrid-index.json</c> rather than a single <c>plugin.json</c> — the
    /// signal the host uses to offer "add as a source" instead of installing a pasted link directly.</summary>
    public async Task<bool> HasIndexAsync(string owner, string repo, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, PluginSourceUrls.IndexUrl(owner, repo));
            using var response = await http.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    private async Task<string> FetchTextAsync(Uri url, long maxBytes, CancellationToken cancellationToken)
    {
        if (!PluginSourceUrls.IsAllowedUrl(url))
            throw new PluginCatalogException(PluginCatalogException.Fetch, $"Not an allowed host: {url.Host}");

        HttpResponseMessage response;
        try
        {
            response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new PluginCatalogException(PluginCatalogException.Fetch, "Could not reach the source. Check your connection.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new PluginCatalogException(PluginCatalogException.Fetch, "The request timed out.", ex);
        }

        using (response)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                throw new PluginCatalogException(PluginCatalogException.Fetch, "Nothing was found at that address.");
            if (!response.IsSuccessStatusCode)
                throw new PluginCatalogException(PluginCatalogException.Fetch, $"The source answered with HTTP {(int)response.StatusCode}.");

            if (response.Content.Headers.ContentLength is { } declared && declared > maxBytes)
                throw new PluginCatalogException(PluginCatalogException.TooLarge, "The response is larger than allowed.");

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var buffer = new MemoryStream();
            var chunk = new byte[8192];
            int read;
            while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
            {
                buffer.Write(chunk, 0, read);
                if (buffer.Length > maxBytes)
                    throw new PluginCatalogException(PluginCatalogException.TooLarge, "The response is larger than allowed.");
            }
            return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
        }
    }

    private static PluginCatalogIndex Parse(string json, string owner, string repo)
    {
        JsonDocument document;
        try { document = JsonDocument.Parse(json); }
        catch (JsonException ex) { throw new PluginCatalogException(PluginCatalogException.Invalid, "macrogrid-index.json is not valid JSON.", ex); }

        using (document)
        {
            var root = document.RootElement;
            var formatVersion = root.TryGetProperty("formatVersion", out var fv) && fv.TryGetInt32(out var f) ? f : 0;
            if (formatVersion != 1)
                throw new PluginCatalogException(PluginCatalogException.Invalid, $"Unsupported index format version: {formatVersion}.");

            var name = GetRequiredString(root, "name", "macrogrid-index.json");
            var author = root.TryGetProperty("author", out var a) && a.ValueKind == JsonValueKind.String ? a.GetString() : null;

            var plugins = new List<PluginCatalogEntry>();
            if (root.TryGetProperty("plugins", out var pluginsElement) && pluginsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var pluginElement in pluginsElement.EnumerateArray())
                    plugins.Add(ParseEntry(pluginElement, owner, repo));
            }

            return new PluginCatalogIndex(formatVersion, name, author, owner, repo, plugins);
        }
    }

    private static PluginCatalogEntry ParseEntry(JsonElement element, string owner, string repo)
    {
        var id = GetRequiredString(element, "id", "a plugin entry");
        var versions = new List<PluginCatalogVersion>();
        if (element.TryGetProperty("versions", out var versionsElement) && versionsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var versionElement in versionsElement.EnumerateArray())
            {
                var version = ParseVersion(versionElement, id);
                if (!Uri.TryCreate(version.Url, UriKind.Absolute, out var url) || !PluginSourceUrls.BelongsToRepo(url, owner, repo))
                    throw new PluginCatalogException(PluginCatalogException.Invalid,
                        $"'{id}' {version.Version}: the download url does not point at {owner}/{repo}'s own releases.");
                versions.Add(version);
            }
        }

        return new PluginCatalogEntry(
            id,
            GetRequiredString(element, "name", id),
            GetString(element, "description"),
            GetString(element, "author"),
            GetString(element, "homepage"),
            GetRequiredString(element, "kind", id),
            versions);
    }

    private static PluginCatalogVersion ParseVersion(JsonElement element, string pluginId)
    {
        var version = GetRequiredString(element, "version", pluginId);
        var sha256 = GetString(element, "sha256");
        if (string.IsNullOrWhiteSpace(sha256))
            throw new PluginCatalogException(PluginCatalogException.Invalid, $"'{pluginId}' {version}: sha256 is required.");
        var size = element.TryGetProperty("size", out var sizeElement) && sizeElement.TryGetInt64(out var s) ? s : 0;
        if (size <= 0)
            throw new PluginCatalogException(PluginCatalogException.Invalid, $"'{pluginId}' {version}: size is required.");

        return new PluginCatalogVersion(
            version,
            GetRequiredString(element, "sdkVersion", pluginId),
            GetRequiredString(element, "minServerVersion", pluginId),
            GetRequiredString(element, "url", pluginId),
            sha256,
            size,
            element.TryGetProperty("permissions", out var perms) && perms.ValueKind == JsonValueKind.Array
                ? [.. perms.EnumerateArray().Where(p => p.ValueKind == JsonValueKind.String).Select(p => p.GetString()!)]
                : null,
            GetString(element, "signature"));
    }

    private static string GetRequiredString(JsonElement element, string name, string context) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!
            : throw new PluginCatalogException(PluginCatalogException.Invalid, $"{context}: '{name}' is missing.");

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
