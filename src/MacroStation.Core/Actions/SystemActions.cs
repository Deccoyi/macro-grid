using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json.Nodes;
using MacroStation.Core.Json;
using MacroStation.Plugin.Abstractions;
using Microsoft.Extensions.Logging;

namespace MacroStation.Core.Actions;

/// <summary>Opens a local application (chosen via the editor's native file browser). Settings: { "target": string, "arguments"?: string }</summary>
public sealed class OpenAction(ILogger<OpenAction> logger) : IActionHandler, IActionDescriptor
{
    public const string TypeId = "core.open";

    public string Type => TypeId;
    public string DisplayName => "Uygulama aç";
    public string Category => "Sistem";
    public string? Description => "Bir uygulama veya dosya açar";
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
            logger.LogWarning(ex, "core.open: '{Target}' açılamadı", target);
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
    public string DisplayName => "URL aç";
    public string Category => "Sistem";
    public string? Description => "Varsayılan tarayıcıda bir adres açar";
    public string? Icon => "link";
    public IReadOnlyList<SettingField> Fields => [];

    public Task ExecuteAsync(ActionContext context, JsonObject settings, CancellationToken cancellationToken)
    {
        var url = settings["url"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(url)) return Task.CompletedTask;

        // Shell-execute on an http(s) URL always hands off to the OS default browser, never this process.
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            logger.LogWarning("core.openUrl: '{Url}' geçerli bir http(s) adresi değil", url);
            return Task.CompletedTask;
        }

        try
        {
            Process.Start(new ProcessStartInfo(uri.ToString()) { UseShellExecute = true });
        }
        catch (Win32Exception ex)
        {
            logger.LogWarning(ex, "core.openUrl: '{Url}' açılamadı", url);
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
    public string DisplayName => "Bekle";
    public string Category => "Sistem";
    public string? Description => "Çoklu aksiyon içinde bekler";
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
