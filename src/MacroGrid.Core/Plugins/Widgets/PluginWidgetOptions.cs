namespace MacroGrid.Core.Plugins.Widgets;

/// <summary>The abilities a plugin widget can declare in its manifest (<c>options</c>). Each needs the person's approval when the plugin is installed;
/// a widget can never use one it did not declare.</summary>
public static class PluginWidgetOptions
{
    /// <summary>The worker is paused, not stopped, when its page is left, so coming back is instant.</summary>
    public const string KeepLoaded = "keepLoaded";

    /// <summary>A small amount of data kept on the device between runs (a safe replacement for cookies).</summary>
    public const string Storage = "storage";

    /// <summary>The widget may show a system notification on the phone.</summary>
    public const string Notifications = "notifications";

    private static readonly string[] All = [KeepLoaded, Storage, Notifications];

    public static bool IsKnown(string option) => All.Contains(option, StringComparer.Ordinal);

    /// <summary>The string under which the approval of one option of one widget is stored next to a plugin's permissions.</summary>
    public static string ApprovalKey(string widgetId, string option) => $"widget:{widgetId}:{option}";

    public static bool IsApprovalKey(string permission) => permission.StartsWith("widget:", StringComparison.Ordinal);
}

/// <summary>Limits of plugin widgets that are checked when a plugin loads. Runtime limits (frame rate, workers, bridge traffic) live with the router.</summary>
public static class PluginWidgetLimits
{
    public const int MaxWidgetsPerPlugin = 16;
    public const int MaxEntryBytes = 2 * 1024 * 1024;
    public const int MaxEntryBytesUnverified = 1024 * 1024;
    public const int MaxAssetBytesPerPlugin = 4 * 1024 * 1024;
    public const int MaxSettingsFields = 24;
    public const int MaxIconBytes = 8 * 1024;
    public const int MaxFps = 60;
    public const int MaxFpsUnverified = 30;
    public const int DefaultFps = 15;
    public const int DefaultWidth = 2;
    public const int DefaultHeight = 2;
    public const int MaxGridSize = 12;
}
