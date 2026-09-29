using MacroGrid.Core.Diagnostics;
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

        return api;
    }
}
