using MacroGrid.Core.Plugins;
using MacroGrid.Core.Plugins.Distribution;
using MacroGrid.Core.Sessions;
using MacroGrid.Plugin.Abstractions;
using Microsoft.AspNetCore.Routing;

namespace MacroGrid.Host.Api;

/// <summary>
/// Discover-tab endpoints: browsing and installing from the official plugin catalog (phase 2 of the plugin
/// distribution plan — added sources and direct links follow in later phases). Loopback-only, like the rest of
/// <c>/api</c>. The HTTP client behind <see cref="PluginCatalogClient"/> and <see cref="PluginPackageDownloader"/>
/// is only ever used from these two endpoints — nothing runs in the background.
/// </summary>
internal static class PluginCatalogApi
{
    public const string OfficialSourceId = "official";

    public static RouteGroupBuilder MapPluginCatalogApi(this RouteGroupBuilder api)
    {
        api.MapGet("/plugin-catalog", async (string? source, PluginCatalogClient client, PluginManager plugins, PluginInstallOriginStore origins, CancellationToken cancellationToken) =>
        {
            if (source != OfficialSourceId)
                return ApiResults.BadRequest($"Unknown source: {source}");

            try
            {
                var index = await client.FetchIndexAsync(PluginSourceUrls.OfficialOwner, PluginSourceUrls.OfficialRepo, cancellationToken);
                return ApiResults.Json(new
                {
                    source = OfficialSourceId,
                    name = index.Name,
                    plugins = index.Plugins.Select(entry => DescribeEntry(entry, plugins, origins)),
                });
            }
            catch (PluginCatalogException ex)
            {
                return ApiResults.Json(new { error = ex.Message, code = ex.Code });
            }
        });

        api.MapPost("/plugin-catalog/install", async (PluginCatalogInstallRequest request, PluginCatalogClient client, PluginCatalogInstaller installer, CancellationToken cancellationToken) =>
        {
            if (request.Source != OfficialSourceId)
                return ApiResults.BadRequest($"Unknown source: {request.Source}");
            if (string.IsNullOrWhiteSpace(request.Id) || string.IsNullOrWhiteSpace(request.Version))
                return ApiResults.BadRequest("id and version are required.");

            try
            {
                var index = await client.FetchIndexAsync(PluginSourceUrls.OfficialOwner, PluginSourceUrls.OfficialRepo, cancellationToken);
                var entry = index.Plugins.FirstOrDefault(p => p.Id == request.Id);
                var version = entry?.Versions.FirstOrDefault(v => v.Version == request.Version);
                if (entry is null || version is null)
                    return ApiResults.BadRequest("That plugin or version is no longer listed by the source.");

                var sourceUrl = $"https://github.com/{PluginSourceUrls.OfficialOwner}/{PluginSourceUrls.OfficialRepo}";
                var result = await installer.InstallAsync(entry, version, sourceUrl, isOfficial: true, cancellationToken);
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

    private sealed record PluginCatalogInstallRequest(string? Source, string? Id, string? Version);
}
