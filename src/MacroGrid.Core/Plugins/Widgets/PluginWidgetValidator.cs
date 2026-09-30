using System.Text.RegularExpressions;
using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Core.Plugins.Widgets;

/// <summary>A widget that passed the checks: its files exist inside the plugin folder and the numbers are in range.</summary>
/// <param name="Fps">The frame rate cap with the default applied and, for an unverified plugin, held to 30.</param>
/// <param name="Assets">Declared path to full path.</param>
public sealed record ValidatedWidget(PluginWidgetManifest Manifest, string EntryPath, IReadOnlyList<KeyValuePair<string, string>> Assets, int Fps, PluginWidgetSize Size, IReadOnlyList<string> Options, string? IconPath = null)
{
    /// <summary>The declared options that start switched off on a placed widget (the manifest says <c>default: false</c>). The person can switch each one per widget.</summary>
    public IReadOnlyList<string> OptionsOffByDefault => Options.Where(o => Manifest.Options is { } declared && declared.TryGetValue(o, out var option) && !option.Default).ToArray();
}

/// <summary>One widget that was refused, with a reason a plugin author can act on.</summary>
public sealed record PluginWidgetProblem(string WidgetId, string Reason);

public sealed record PluginWidgetValidation(IReadOnlyList<ValidatedWidget> Widgets, IReadOnlyList<PluginWidgetProblem> Problems);

/// <summary>
/// Checks the <c>widgets</c> of a plugin manifest when the plugin loads. A bad widget is left out on its own (with a reason) and the plugin and its
/// other widgets keep working. Nothing of a widget's code is run here; the files are only measured.
/// </summary>
public static partial class PluginWidgetValidator
{
    private static readonly string[] AssetExtensions = [".png", ".jpg", ".jpeg", ".webp", ".woff2"];

    public static PluginWidgetValidation Validate(string pluginDir, PluginManifest manifest, bool verified)
    {
        var widgets = new List<ValidatedWidget>();
        var problems = new List<PluginWidgetProblem>();
        if (manifest.Widgets is not { Length: > 0 } declared) return new PluginWidgetValidation(widgets, problems);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var assetBytes = 0L;
        for (var i = 0; i < declared.Length; i++)
        {
            var widget = declared[i];
            var label = string.IsNullOrEmpty(widget?.Id) ? $"#{i + 1}" : widget.Id;
            if (i >= PluginWidgetLimits.MaxWidgetsPerPlugin)
            {
                problems.Add(new PluginWidgetProblem(label, $"A plugin can have at most {PluginWidgetLimits.MaxWidgetsPerPlugin} widgets"));
                continue;
            }
            var reason = Check(pluginDir, widget, verified, seen, ref assetBytes, out var validated);
            if (reason is null) widgets.Add(validated!);
            else problems.Add(new PluginWidgetProblem(label, reason));
        }
        return new PluginWidgetValidation(widgets, problems);
    }

    private static string? Check(string dir, PluginWidgetManifest? widget, bool verified, HashSet<string> seen, ref long assetBytes, out ValidatedWidget? result)
    {
        result = null;
        if (widget is null) return "Empty widget entry";
        if (!IdPattern().IsMatch(widget.Id ?? "")) return "The id may only use letters, digits, '-' and '_' (at most 64)";
        if (!seen.Add(widget.Id!)) return "Another widget of this plugin has the same id";
        if (string.IsNullOrWhiteSpace(widget.Name) || widget.Name.Length > 60) return "The name is missing or longer than 60 characters";
        if (widget.Description is { Length: > 200 }) return "The description is longer than 200 characters";
        if (widget.Category is { Length: > 40 }) return "The category is longer than 40 characters";

        var size = widget.Size ?? new PluginWidgetSize(PluginWidgetLimits.DefaultWidth, PluginWidgetLimits.DefaultHeight);
        if (size.W < 1 || size.H < 1 || size.W > PluginWidgetLimits.MaxGridSize || size.H > PluginWidgetLimits.MaxGridSize)
            return $"The size must be between 1 and {PluginWidgetLimits.MaxGridSize} cells each way";

        var fps = widget.Fps ?? PluginWidgetLimits.DefaultFps;
        if (fps < 1 || fps > PluginWidgetLimits.MaxFps) return $"fps must be between 1 and {PluginWidgetLimits.MaxFps}";
        if (!verified) fps = Math.Min(fps, PluginWidgetLimits.MaxFpsUnverified);

        var options = new List<string>();
        foreach (var name in widget.Options?.Keys.ToArray() ?? [])
        {
            if (name == "notifications") return "The 'notifications' option is not supported (a widget cannot show notifications)";
            if (!PluginWidgetOptions.IsKnown(name)) return $"Unknown option '{name}'";
            options.Add(name);
        }

        if (widget.Settings is { } settings)
        {
            if (settings.Length > PluginWidgetLimits.MaxSettingsFields) return $"At most {PluginWidgetLimits.MaxSettingsFields} settings";
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var field in settings)
            {
                if (field is null || string.IsNullOrWhiteSpace(field.Key)) return "A setting has no key";
                if (!keys.Add(field.Key)) return $"The setting key '{field.Key}' is used twice";
                if (field.Kind is SettingFieldKind.Password or SettingFieldKind.File)
                    return $"The setting '{field.Key}' cannot be a {field.Kind} field (widget settings are saved in the profile, which people share)";
            }
        }

        var maxEntry = verified ? PluginWidgetLimits.MaxEntryBytes : PluginWidgetLimits.MaxEntryBytesUnverified;
        var entry = ResolveFile(dir, widget.Entry, [".js"], out var entryError);
        if (entry is null) return $"entry: {entryError}";
        var entryLength = new FileInfo(entry).Length;
        if (entryLength == 0) return "entry: the file is empty";
        if (entryLength > maxEntry) return $"entry: the script is larger than {maxEntry / 1024} KB";

        string? iconPath = null;
        if (!string.IsNullOrWhiteSpace(widget.Icon))
        {
            iconPath = ResolveFile(dir, widget.Icon, [".svg"], out var iconError);
            if (iconPath is null) return $"icon: {iconError}";
            var iconProblem = CheckIcon(iconPath);
            if (iconProblem is not null) return $"icon: {iconProblem}";
        }

        var assets = new List<KeyValuePair<string, string>>();
        foreach (var declared in widget.Assets ?? [])
        {
            var path = ResolveFile(dir, declared, AssetExtensions, out var error);
            if (path is null) return $"asset '{declared}': {error}";
            assetBytes += new FileInfo(path).Length;
            if (assetBytes > PluginWidgetLimits.MaxAssetBytesPerPlugin) return $"The assets of the plugin are larger than {PluginWidgetLimits.MaxAssetBytesPerPlugin / (1024 * 1024)} MB in total";
            assets.Add(new KeyValuePair<string, string>(declared, path));
        }

        result = new ValidatedWidget(widget, entry, assets, fps, size, options, iconPath);
        return null;
    }

    /// <summary>The full path of a file that lies inside the plugin folder, is not a link, has an allowed extension and exists; otherwise null and why.</summary>
    private static string? ResolveFile(string dir, string? relative, string[] extensions, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(relative)) { error = "no path"; return null; }
        if (Path.IsPathRooted(relative) || relative.Contains("..") || relative.Contains(':')) { error = "the path must be relative and stay inside the plugin folder"; return null; }
        var root = Path.GetFullPath(dir) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.Combine(dir, relative));
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) { error = "the path must stay inside the plugin folder"; return null; }
        if (!extensions.Contains(Path.GetExtension(full), StringComparer.OrdinalIgnoreCase)) { error = $"only {string.Join(", ", extensions)} files are allowed"; return null; }
        var info = new FileInfo(full);
        if (!info.Exists) { error = "the file was not found"; return null; }
        if (info.Attributes.HasFlag(FileAttributes.ReparsePoint)) { error = "links are not allowed"; return null; }
        return full;
    }

    /// <summary>An icon is drawn by the editor as an image, so scripts could not run in it anyway; it is still kept to plain shapes: no scripts, no embedded pages or images, no links to anywhere.</summary>
    public static string? CheckIcon(string path)
    {
        var info = new FileInfo(path);
        if (info.Length == 0) return "the file is empty";
        if (info.Length > PluginWidgetLimits.MaxIconBytes) return $"the file is larger than {PluginWidgetLimits.MaxIconBytes / 1024} KB";
        string text;
        try { text = File.ReadAllText(path); }
        catch (IOException) { return "the file could not be read"; }
        if (!SvgStart().IsMatch(text)) return "the file is not an SVG";
        if (ForbiddenSvg().IsMatch(text)) return "an SVG icon may not contain scripts, embedded pages or images, event handlers or links";
        return null;
    }

    [GeneratedRegex(@"^\s*(<\?xml[^>]*\?>\s*)?(<!--.*?-->\s*)*<svg[\s>]", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex SvgStart();

    // Anything that can run code or reach outside the file. The svg namespace itself (xmlns="http://www.w3.org/...") is fine and not matched.
    [GeneratedRegex(@"<\s*(script|foreignObject|image|iframe|object|embed|use|a|animate|set)\b|\bon\w+\s*=|javascript:|(href|src)\s*=|url\(\s*['""]?\s*(https?:|data:|//)|<!ENTITY|<!DOCTYPE", RegexOptions.IgnoreCase)]
    private static partial Regex ForbiddenSvg();

    [GeneratedRegex("^[A-Za-z0-9_-]{1,64}$")]
    private static partial Regex IdPattern();
}
