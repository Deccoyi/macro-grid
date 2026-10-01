using System.Runtime.InteropServices;
using MacroGrid.Core.Devices;
using MacroGrid.Core.Diagnostics;
using MacroGrid.Core.Plugins;
using MacroGrid.Core.Preferences;
using MacroGrid.Core.Profiles;
using MacroGrid.Core.Sessions;
using MacroGrid.Host.Ui;
using Microsoft.AspNetCore.Routing;

namespace MacroGrid.Host.Api;

/// <summary>The Help menu's "Export logs": what the export would contain, and the export itself. Loopback-only, like the rest of <c>/api</c>.
/// Nothing is uploaded; the file is written only where the person points the Save dialog.</summary>
internal static class DiagnosticsApi
{
    public static RouteGroupBuilder MapDiagnosticsApi(this RouteGroupBuilder api)
    {
        api.MapGet("/diagnostics/logs-preview", () =>
        {
            var files = LogExport.List(LogsDir);
            var total = files.Sum(f => f.Bytes);
            return ApiResults.Json(new { files, totalBytes = total, maxBytes = LogExport.MaxBytes });
        });

        api.MapPost("/diagnostics/export-logs", async (
            ProblemList problems, DeviceStore devices, ProfileStore profiles, PluginManager plugins, PreferencesStore preferences, IUiDialogService dialogs) =>
        {
            var redactor = new Redactor(RedactionContext.ForThisPc(ProfileStore.DefaultDataDir, devices.All.Select(d => d.Name)));
            var now = DateTimeOffset.Now;
            var all = profiles.All;
            var info = new LogExportInfo(
                ClientHub.ServerVersion, RuntimeInformation.OSDescription, RuntimeInformation.FrameworkDescription, preferences.Get().Language,
                plugins.Plugins.Select(p => new LogExportPlugin(p.Id, p.Name, p.Version, p.Status.ToString())).ToList(),
                all.Count, all.Sum(p => p.Pages.Count), devices.All.Count,
                problems.Snapshot().Select(p => new ExportLine(
                    p.Severity.ToString().ToLowerInvariant(), p.Code, p.Message, p.SourceName, p.Count, FirstAt: p.FirstAt, LastAt: p.LastAt)).ToList(),
                now);

            // Built off the request thread: reading and redacting up to 10 MB of text takes a moment. The Save dialog comes last.
            var zip = await Task.Run(() => LogExport.Build(LogsDir, info, redactor));
            var path = await dialogs.SaveFileAsync("Export logs", $"macro-grid-logs-{now:yyyyMMdd-HHmm}.zip", "Zip file (*.zip)|*.zip", "zip", zip);
            return Results.Json(new { path });
        });

        return api;
    }

    private static string LogsDir => Path.Combine(ProfileStore.DefaultDataDir, "logs");
}
