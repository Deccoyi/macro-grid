using System.Text.Json;
using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Core.Plugins.Distribution;

/// <summary>
/// The install pipeline shared by every distribution method (official catalog, added source, direct link):
/// download, verify (hash, and signature for the official source), safely unzip to a staging folder, check the
/// zip's own <c>plugin.json</c> against what the catalog promised, install through the existing
/// <see cref="PluginManager.InstallFromFolderAsync"/>, record where it came from, and always clean up staging.
/// See "5. Host install pipeline" in the plugin distribution plan.
/// </summary>
public sealed class PluginCatalogInstaller(
    PluginPackageDownloader downloader,
    PluginManager pluginManager,
    PluginInstallOriginStore originStore,
    string stagingRoot)
{
    private static readonly JsonSerializerOptions ManifestJson = new(JsonSerializerDefaults.Web);

    public async Task<PluginInstallResult> InstallAsync(
        PluginCatalogEntry entry, PluginCatalogVersion version, string sourceUrl, bool isOfficial, CancellationToken cancellationToken)
    {
        var bytes = await downloader.DownloadAsync(version, requireOfficialSignature: isOfficial, cancellationToken);

        Directory.CreateDirectory(stagingRoot);
        var stagingDir = Path.Combine(stagingRoot, Guid.NewGuid().ToString("N"));
        try
        {
            PluginZip.ExtractSafely(bytes, stagingDir);

            var manifestPath = Path.Combine(stagingDir, "plugin.json");
            if (!File.Exists(manifestPath))
                throw new PluginDownloadException(PluginDownloadException.Verify, "The package has no plugin.json.");

            PluginManifest manifest;
            try
            {
                manifest = JsonSerializer.Deserialize<PluginManifest>(await File.ReadAllTextAsync(manifestPath, cancellationToken), ManifestJson)
                    ?? throw new JsonException("plugin.json is empty");
            }
            catch (JsonException ex)
            {
                throw new PluginDownloadException(PluginDownloadException.Verify, "The package's plugin.json could not be read.", ex);
            }

            CrossCheck(entry, version, manifest);

            var result = await pluginManager.InstallFromFolderAsync(stagingDir);
            originStore.Set(result.Id, new PluginInstallOrigin(sourceUrl, version.Version, isOfficial ? PluginTrust.Official : PluginTrust.ThirdParty));
            return result;
        }
        finally
        {
            TryDeleteDirectory(stagingDir);
        }
    }

    /// <summary>The zip's own manifest must match what the index (or single-plugin plugin.json) promised, exactly —
    /// otherwise a compromised or mismatched package could claim compatibility or permissions it does not have.</summary>
    private static void CrossCheck(PluginCatalogEntry entry, PluginCatalogVersion version, PluginManifest manifest)
    {
        void Require(bool ok, string what)
        {
            if (!ok) throw new PluginDownloadException(PluginDownloadException.Verify, $"The package does not match the source's listing ({what}).");
        }

        Require(string.Equals(manifest.Id, entry.Id, StringComparison.Ordinal), "id");
        Require(string.Equals(manifest.Version, version.Version, StringComparison.Ordinal), "version");
        Require(string.Equals(manifest.SdkVersion, version.SdkVersion, StringComparison.Ordinal), "sdkVersion");
        Require(string.Equals(manifest.MinServerVersion, version.MinServerVersion, StringComparison.Ordinal), "minServerVersion");
        Require(string.Equals(manifest.Kind.ToString(), entry.Kind, StringComparison.OrdinalIgnoreCase), "kind");

        var declared = new HashSet<string>(manifest.Permissions ?? [], StringComparer.Ordinal);
        var listed = new HashSet<string>(version.Permissions ?? [], StringComparer.Ordinal);
        Require(declared.SetEquals(listed), "permissions");
    }

    private static void TryDeleteDirectory(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A locked staging file is not worth failing the install over; it is orphaned under plugins-staging.
        }
    }
}
