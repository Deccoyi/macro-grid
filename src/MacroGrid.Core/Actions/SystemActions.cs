using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json.Nodes;
using MacroGrid.Core.Json;
using MacroGrid.Plugin.Abstractions;
using Microsoft.Extensions.Logging;

namespace MacroGrid.Core.Actions;

/// <summary>Opens a local application (chosen via the editor's native file browser). Settings: { "target": string, "arguments"?: string }</summary>
public sealed class OpenAction(ILogger<OpenAction> logger) : IActionHandler, IActionDescriptor
{
    public const string TypeId = "core.open";

    public string Type => TypeId;
    public string DisplayName => "Open app";
    public string Category => "System";
    public string? Description => "Opens an app or a file";
    public string? Icon => "app-window";
    public IReadOnlyList<SettingField> Fields => [];

    public Task ExecuteAsync(ActionContext context, JsonObject settings, CancellationToken cancellationToken)
    {
        var target = settings["target"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(target)) return Task.CompletedTask;

        try
        {
            Process.Start(new ProcessStartInfo(target)
            {
                Arguments = settings["arguments"]?.GetValue<string>() ?? "",
                UseShellExecute = true,
            });
        }
        catch (Exception ex) when (ex is Win32Exception or FileNotFoundException)
        {
            logger.LogWarning(ex, "core.open: '{Target}' could not be opened", target);
        }

        return Task.CompletedTask;
    }

    public static JsonObject Settings(string target, string? arguments = null) =>
        arguments is null ? new JsonObject { ["target"] = target } : new JsonObject { ["target"] = target, ["arguments"] = arguments };
}

/// <summary>Opens a URL in the system's default browser. Settings: { "url": string }</summary>
public sealed class OpenUrlAction(ILogger<OpenUrlAction> logger) : IActionHandler, IActionDescriptor
{
    public const string TypeId = "core.openUrl";

    public string Type => TypeId;
    public string DisplayName => "Open URL";
    public string Category => "System";
    public string? Description => "Opens an address in the default browser";
    public string? Icon => "link";
    public IReadOnlyList<SettingField> Fields => [];

    public Task ExecuteAsync(ActionContext context, JsonObject settings, CancellationToken cancellationToken)
    {
        var url = settings["url"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(url)) return Task.CompletedTask;

        // Shell-execute on an http(s) URL always hands off to the OS default browser, never this process.
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            logger.LogWarning("core.openUrl: '{Url}' is not a valid http(s) address", url);
            return Task.CompletedTask;
        }

        try
        {
            Process.Start(new ProcessStartInfo(uri.ToString()) { UseShellExecute = true });
        }
        catch (Win32Exception ex)
        {
            logger.LogWarning(ex, "core.openUrl: '{Url}' could not be opened", url);
        }

        return Task.CompletedTask;
    }

    public static JsonObject Settings(string url) => new() { ["url"] = url };
}

/// <summary>Pauses a multi-action macro. Settings: { "ms": number }, clamped to [0, 60000].</summary>
public sealed class DelayAction : IActionHandler, IActionDescriptor
{
    public const string TypeId = "core.delay";
    private const int MaxMs = 60_000;

    public string Type => TypeId;
    public string DisplayName => "Wait";
    public string Category => "System";
    public string? Description => "Waits inside a multi-action";
    public string? Icon => "clock";
    public IReadOnlyList<SettingField> Fields => [];

    public Task ExecuteAsync(ActionContext context, JsonObject settings, CancellationToken cancellationToken)
    {
        var ms = settings["ms"].AsDouble() ?? 0;
        var clamped = Math.Clamp((int)ms, 0, MaxMs);
        return clamped == 0 ? Task.CompletedTask : Task.Delay(clamped, cancellationToken);
    }

    public static JsonObject Settings(int ms) => new() { ["ms"] = ms };
}
