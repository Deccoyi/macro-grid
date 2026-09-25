using MacroGrid.Core.Plugins;
using MacroGrid.Core.Plugins.Distribution;
using MacroGrid.Core.Sessions;
using MacroGrid.Plugin.Abstractions;
using Microsoft.AspNetCore.Routing;

namespace MacroGrid.Host.Api;

/// <summary>
/// Discover-tab endpoints: browsing and installing from the official catalog (phase 2) and from added
/// third-party multi-plugin sources (phase 3) of the plugin distribution plan. Direct single-plugin links
/// (phase 4) follow later. Loopback-only, like the rest of <c>/api</c>. The HTTP client behind <see
/// cref="PluginCatalogClient"/> and <see cref="PluginPackageDownloader"/> is only ever used from these
/// endpoints — nothing runs in the background.
/// </summary>
internal static class PluginCatalogApi
{
    public const string OfficialSourceId = "official";

    public static RouteGroupBuilder MapPluginCatalogApi(this RouteGroupBuilder api)
    {
        api.MapGet("/plugin-sources", (PluginSourceStore sources) => ApiResults.Json(new
        {
            official = new { id = OfficialSourceId, owner = PluginSourceUrls.OfficialOwner, repo = PluginSourceUrls.OfficialRepo },
            added = sources.All().Select(s => new { s.Id, s.Owner, s.Repo, s.Name, s.AddedAt }),
        }));

        // Adding a source is a network call (it fetches the index once, to validate the repo actually has one
        // and to read its display name) — still only ever triggered by the user clicking "Add source".
        api.MapPost("/plugin-sources", async (PluginAddSourceRequest request, PluginCatalogClient client, PluginSourceStore sources, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Url) || !PluginSourceUrls.TryParseRepo(request.Url, out var owner, out var repo))
                return ApiResults.BadRequest("That doesn't look like a GitHub repository (expected a URL or \"owner/repo\").");
            if (string.Equals(owner, PluginSourceUrls.OfficialOwner, StringComparison.OrdinalIgnoreCase)
                && string.Equals(repo, PluginSourceUrls.OfficialRepo, StringComparison.OrdinalIgnoreCase))
                return ApiResults.BadRequest("That is already the built-in official source.");

            try
            {
                var index = await client.FetchIndexAsync(owner, repo, cancellationToken);
                var source = new PluginSource($"{owner}/{repo}".ToLowerInvariant(), owner, repo, index.Name, DateTimeOffset.UtcNow);
                sources.Add(source);
                return ApiResults.Json(source);
            }
            catch (PluginCatalogException ex)
            {
                return ApiResults.Json(new { error = ex.Message, code = ex.Code });
            }
        });

        api.MapDelete("/plugin-sources/{id}", (string id, PluginSourceStore sources) =>
            sources.Remove(id) ? Results.NoContent() : Results.NotFound());

        api.MapGet("/plugin-catalog", async (string? source, PluginCatalogClient client, PluginSourceStore sources, PluginManager plugins, PluginInstallOriginStore origins, CancellationToken cancellationToken) =>
        {
            if (!TryResolveSource(source, sources, out var owner, out var repo, out var isOfficial))
                return ApiResults.BadRequest($"Unknown source: {source}");

            try
            {
                var index = await client.FetchIndexAsync(owner, repo, cancellationToken);
                return ApiResults.Json(new
                {
                    source,
                    name = index.Name,
                    official = isOfficial,
                    plugins = index.Plugins.Select(entry => DescribeEntry(entry, plugins, origins)),
                });
            }
            catch (PluginCatalogException ex)
            {
                return ApiResults.Json(new { error = ex.Message, code = ex.Code });
            }
        });

        api.MapPost("/plugin-catalog/install", async (PluginCatalogInstallRequest request, PluginCatalogClient client, PluginSourceStore sources, PluginCatalogInstaller installer, CancellationToken cancellationToken) =>
        {
            if (!TryResolveSource(request.Source, sources, out var owner, out var repo, out var isOfficial))
                return ApiResults.BadRequest($"Unknown source: {request.Source}");
            if (string.IsNullOrWhiteSpace(request.Id) || string.IsNullOrWhiteSpace(request.Version))
                return ApiResults.BadRequest("id and version are required.");

            try
            {
                var index = await client.FetchIndexAsync(owner, repo, cancellationToken);
                var entry = index.Plugins.FirstOrDefault(p => p.Id == request.Id);
                var version = entry?.Versions.FirstOrDefault(v => v.Version == request.Version);
                if (entry is null || version is null)
                    return ApiResults.BadRequest("That plugin or version is no longer listed by the source.");

                var sourceUrl = $"https://github.com/{owner}/{repo}";
                var result = await installer.InstallAsync(entry, version, sourceUrl, isOfficial, cancellationToken);
                return ApiResults.Json(new { installed = true, id = result.Id, name = result.Name, status = result.Plugin.Status, detail = result.Plugin.Detail });
            }
            catch (Exception ex) when (ex is PluginCatalogException or PluginDownloadException or InvalidOperationException or IOException or UnauthorizedAccessException)
            {
                var code = (ex as PluginCatalogException)?.Code ?? (ex as PluginDownloadException)?.Code;
                return ApiResults.Json(new { installed = false, error = ex.Message, code });
            }
        });

        return api;
    }

    /// <summary>"official" resolves to the hard-coded official repository; anything else must be a saved
    /// source's id (<c>"&lt;owner&gt;/&lt;repo&gt;"</c>).</summary>
    private static bool TryResolveSource(string? source, PluginSourceStore sources, out string owner, out string repo, out bool isOfficial)
    {
        if (source == OfficialSourceId)
        {
            owner = PluginSourceUrls.OfficialOwner;
            repo = PluginSourceUrls.OfficialRepo;
            isOfficial = true;
            return true;
        }

        isOfficial = false;
        if (source is not null && sources.Find(source) is { } saved)
        {
            owner = saved.Owner;
            repo = saved.Repo;
            return true;
        }

        owner = "";
        repo = "";
        return false;
    }

    private static object DescribeEntry(PluginCatalogEntry entry, PluginManager plugins, PluginInstallOriginStore origins)
    {
        var installed = plugins.Plugins.FirstOrDefault(p => p.Id == entry.Id);
        var origin = origins.Get(entry.Id);

        var compatible = entry.Versions
            .Where(v => SemVer.SatisfiesCaret(PluginSdk.Version, v.SdkVersion) && SemVer.SatisfiesMinimum(ClientHub.ServerVersion, v.MinServerVersion))
            .OrderByDescending(v => v.Version, Comparer<string>.Create(SemVer.CompareVersionStrings))
            .FirstOrDefault();
        var latest = entry.Versions.OrderByDescending(v => v.Version, Comparer<string>.Create(SemVer.CompareVersionStrings)).FirstOrDefault();

        return new
        {
            entry.Id,
            entry.Name,
            entry.Description,
            entry.Author,
            entry.Homepage,
            entry.Kind,
            latestVersion = latest?.Version,
            installableVersion = compatible?.Version,
            compatible = compatible is not null,
            permissions = compatible?.Permissions ?? latest?.Permissions ?? [],
            installed = installed is not null,
            installedVersion = installed?.Version,
            updateAvailable = installed is not null && compatible is not null
                && SemVer.CompareVersionStrings(compatible.Version, installed.Version) > 0,
            trust = origin?.Trust.ToString() ?? (installed is not null ? PluginTrust.Local.ToString() : null),
        };
    }

    private sealed record PluginAddSourceRequest(string? Url);

    private sealed record PluginCatalogInstallRequest(string? Source, string? Id, string? Version);
}
