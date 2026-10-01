using MacroGrid.Core.Devices;
using MacroGrid.Core.Diagnostics;
using MacroGrid.Core.Profiles;
using MacroGrid.Core.Sessions;
using MacroGrid.Host.Ui;
using Microsoft.AspNetCore.Routing;

namespace MacroGrid.Host.Api;

/// <summary>The editor's Error List: what the server has seen go wrong (a plugin's refused key press, a plugin that did not load, a call that
/// failed), counted per message. Loopback-only, like the rest of <c>/api</c>.</summary>
internal static class ProblemApi
{
    public static RouteGroupBuilder MapProblemApi(this RouteGroupBuilder api)
    {
        api.MapGet("/problems", (ProblemList problems) => ApiResults.Json(new
        {
            version = problems.Version,
            problems = problems.Snapshot().Select(p => new
            {
                p.Id, p.Source, p.SourceName, severity = p.Severity.ToString().ToLowerInvariant(), p.Code, p.Message, p.Count, p.FirstAt, p.LastAt,
            }),
        }));

        api.MapPost("/problems/clear", (ProblemList problems, string? source) =>
        {
            problems.Clear(string.IsNullOrEmpty(source) ? null : source);
            return Results.NoContent();
        });

        // The Error List as a redacted JSON document: the editor sends its own lines (as text), the server adds its own. "text" answers with the
        // document (the editor puts it on the clipboard), "file" shows the Save dialog.
        api.MapPost("/problems/export", async (HttpRequest request, ProblemList problems, DeviceStore devices, IUiDialogService dialogs) =>
        {
            if (request.ContentLength > ProblemExport.MaxBodyBytes) return ApiResults.BadRequest("The list is too big to export.");
            var (valid, body) = await ApiResults.ReadJsonAsync<ProblemExportRequest>(request);
            if (!valid || body is null) return ApiResults.InvalidJson();
            var lines = body.Lines ?? [];
            if (ProblemExport.Refusal(lines.Count) is { } refusal) return ApiResults.BadRequest(refusal);

            var redactor = new Redactor(RedactionContext.ForThisPc(ProfileStore.DefaultDataDir, devices.All.Select(d => d.Name)));
            var now = DateTimeOffset.Now;
            var text = ProblemExport.Build(lines, problems.Snapshot(), redactor, now, ClientHub.ServerVersion);
            if (body.To != "file") return Results.Json(new { text });

            var path = await dialogs.SaveFileAsync("Export Error List", $"macro-grid-problems-{now:yyyyMMdd-HHmm}.json",
                "JSON file (*.json)|*.json", "json", System.Text.Encoding.UTF8.GetBytes(text));
            return Results.Json(new { path });
        });

        return api;
    }

    private sealed record ProblemExportRequest(string? To, List<ExportLine>? Lines);
}
