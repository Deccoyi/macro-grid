using System.Text.Json;
using MacroGrid.Core.Plugins.Distribution;
using MacroGrid.Core.Plugins.Js;
using MacroGrid.Core.Plugins.Widgets;
using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Core.Plugins;

public enum PluginFindingLevel
{
    Error,
    Warning,
}

public sealed record PluginFinding(PluginFindingLevel Level, string Message);

/// <summary>What was found in a plugin folder. <see cref="Manifest"/> is null when plugin.json could not be read at all.</summary>
public sealed record PluginFolderReport(PluginManifest? Manifest, IReadOnlyList<PluginFinding> Findings)
{
    public bool HasErrors => Findings.Any(f => f.Level == PluginFindingLevel.Error);
}

/// <summary>
/// A static check of a plugin folder before it is packaged or installed. It is a thin list of calls to the loader's own functions
/// (the same id, version, compatibility, entry, permission, widget and icon rules), so a folder it accepts is one the loader accepts;
/// nothing is run. A folder with an error would not load, a warning is something the loader tolerates but an author should look at.
/// </summary>
public static class PluginFolderValidator
{
    private const long MaxPackageBytes = PluginZip.MaxTotalBytes;

    public static PluginFolderReport Validate(string dir, string macroGridVersion)
    {
        var findings = new List<PluginFinding>();
        void Error(string message) => findings.Add(new(PluginFindingLevel.Error, message));
        void Warn(string message) => findings.Add(new(PluginFindingLevel.Warning, message));

        if (!Directory.Exists(dir))
        {
            Error($"Folder not found: {dir}");
            return new(null, findings);
        }

        PluginManifest manifest;
        try
        {
            manifest = PluginManager.PeekManifest(dir);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
        {
            Error(ex is InvalidOperationException ? "plugin.json not found" : $"plugin.json could not be read: {ex.Message}");
            return new(null, findings);
        }

        if (!PluginManager.IsValidPluginId(manifest.Id))
            Error($"The id '{manifest.Id}' is not allowed: use up to 64 letters, digits, '.', '-' or '_', starting with a letter or digit, not ending with '.', and not a reserved name");
        if (string.IsNullOrWhiteSpace(manifest.Name))
            Error("The name is empty");
        if (!SemVer.TryParse(manifest.Version, out _))
            Error($"The version '{manifest.Version}' must be MAJOR.MINOR.PATCH such as \"1.0.0\"");

        CheckCompatibility(manifest, macroGridVersion, Error, Warn);
        CheckEntry(dir, manifest, Error);
        if (manifest.Kind == PluginKind.Js) CheckPermissions(manifest, Error, Warn);
        CheckWidgets(dir, manifest, Error);
        CheckIcon(dir, manifest, Warn);
        CheckCatalogFields(manifest, Warn);
        CheckLocales(dir, Warn);
        CheckFiles(dir, Error, Warn);

        return new(manifest, findings);
    }

    private static void CheckCompatibility(PluginManifest manifest, string macroGridVersion, Action<string> error, Action<string> warn)
    {
        if (!string.IsNullOrWhiteSpace(manifest.MacroGrid) || !string.IsNullOrWhiteSpace(manifest.SdkVersion) || !string.IsNullOrWhiteSpace(manifest.MinServerVersion))
            warn("macroGrid, sdkVersion and minServerVersion are legacy fields; use minMacroGrid");

        var result = PluginCompatibility.Check(macroGridVersion, manifest.MinMacroGrid, manifest.MacroGrid, manifest.SdkVersion);
        if (!result.Compatible) error(result.Reason ?? "Not compatible");
    }

    private static void CheckEntry(string dir, PluginManifest manifest, Action<string> error)
    {
        var entryPath = PluginManager.ResolveEntryPath(dir, manifest.Entry);
        if (entryPath is null)
        {
            error($"Entry file not found: {manifest.Entry}");
            return;
        }

        if (manifest.Kind != PluginKind.Js) return;
        try
        {
            Jint.Engine.PrepareScript(File.ReadAllText(entryPath), manifest.Entry);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            error($"{manifest.Entry} does not parse: {ex.Message}");
        }
    }

    private static void CheckPermissions(PluginManifest manifest, Action<string> error, Action<string> warn)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var permission in manifest.Permissions ?? [])
        {
            if (!JsPermissions.IsKnown(permission))
            {
                error($"Unknown permission '{permission}'");
                continue;
            }
            if (!seen.Add(permission)) warn($"The permission '{permission}' is listed twice");

            var colon = permission.LastIndexOf(':');
            if (permission.Contains(':') && int.TryParse(permission[(colon + 1)..], out var port) && port is < 1 or > 65535)
                error($"The permission '{permission}' has a port outside 1-65535");
        }
    }

    private static void CheckWidgets(string dir, PluginManifest manifest, Action<string> error)
    {
        foreach (var problem in PluginWidgetValidator.Validate(dir, manifest, verified: false).Problems)
            error($"Widget {problem.WidgetId}: {problem.Reason}");
    }

    private static void CheckIcon(string dir, PluginManifest manifest, Action<string> warn)
    {
        if (!string.IsNullOrWhiteSpace(manifest.Icon) && PluginManager.ResolveIconPath(dir, manifest) is null)
            warn($"The icon '{manifest.Icon}' is ignored: it must be an .svg or .png file of at most 100 KB inside the plugin folder");
    }

    private static void CheckCatalogFields(PluginManifest manifest, Action<string> warn)
    {
        if (manifest.Category is { } category && CatalogText.Clean(category, CatalogText.MaxCategoryLength) != category.Trim())
            warn($"The category is cut to {CatalogText.MaxCategoryLength} characters in the store");
        if (manifest.Tags is { } tags && (tags.Length > CatalogText.MaxTags || tags.Any(t => t is null || t.Length > CatalogText.MaxTagLength)))
            warn($"Only {CatalogText.MaxTags} tags of up to {CatalogText.MaxTagLength} characters are shown in the store");
    }

    private static void CheckLocales(string dir, Action<string> warn)
    {
        var localesDir = Path.Combine(dir, "locales");
        if (!Directory.Exists(localesDir)) return;
        foreach (var file in Directory.EnumerateFiles(localesDir, "*.json"))
        {
            try
            {
                if (JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(file)) is null)
                    warn($"locales/{Path.GetFileName(file)} is empty");
            }
            catch (Exception ex) when (ex is JsonException or IOException)
            {
                warn($"locales/{Path.GetFileName(file)} is ignored: it is not an object of text to text");
            }
        }
    }

    private static void CheckFiles(string dir, Action<string> error, Action<string> warn)
    {
        foreach (var name in new[] { "settings.json", "storage.json" })
            if (File.Exists(Path.Combine(dir, name)))
                warn($"{name} is the plugin's own saved data and is left out of a package");

        var total = 0L;
        foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
        {
            var length = new FileInfo(file).Length;
            if (length > PluginZip.MaxEntryBytes)
                error($"{Path.GetRelativePath(dir, file)} is larger than {PluginZip.MaxEntryBytes / 1024 / 1024} MB");
            total += length;
        }
        if (total > MaxPackageBytes)
            error($"The folder is larger than {MaxPackageBytes / 1024 / 1024} MB");

        if (!Directory.EnumerateFiles(dir, "LICENSE*").Any())
            warn("There is no LICENSE file");
    }
}
