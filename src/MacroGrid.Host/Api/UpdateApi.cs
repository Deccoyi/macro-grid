using MacroGrid.Core.Updates;
using MacroGrid.Host.Updates;
using Microsoft.AspNetCore.Routing;

namespace MacroGrid.Host.Api;

/// <summary>Editor endpoints for the server's own updates: what is available, a manual check, and the person's answer ("Later", "Skip this version").</summary>
internal static class UpdateApi
{
    public static RouteGroupBuilder MapUpdateApi(this RouteGroupBuilder api)
    {
        var update = api.MapGroup("/update");

        update.MapGet("", (UpdateService updates, UpdateInstaller installer) => ApiResults.Json(Snapshot(updates, installer)));

        update.MapPost("/check", async (UpdateService updates, UpdateInstaller installer, CancellationToken cancellationToken) =>
        {
            var result = await updates.CheckNowAsync(cancellationToken);
            var outcome = result.Outcome switch { UpdateCheckOutcome.Available => "available", UpdateCheckOutcome.UpToDate => "upToDate", _ => "failed" };
            return ApiResults.Json(new { outcome, snapshot = Snapshot(updates, installer) });
        });

        // Returns at once; the editor follows the download in GET /api/update ("install").
        update.MapPost("/install", (UpdateInstaller installer) =>
            installer.TryStart(out var refusal) ? Results.Accepted() : ApiResults.BadRequest(refusal));

        update.MapPost("/snooze", (UpdateService updates) =>
            updates.Snooze() ? Results.NoContent() : ApiResults.BadRequest("No update is available."));

        update.MapPost("/skip", (UpdateService updates) =>
            updates.Skip() ? Results.NoContent() : ApiResults.BadRequest("No update is available."));

        return api;
    }

    /// <summary>The update state plus the state of "Install now"; "Install now" is offered only when the release has an installer and this copy can be upgraded in place.</summary>
    private static UpdateSnapshot Snapshot(UpdateService updates, UpdateInstaller installer)
    {
        var snapshot = updates.Snapshot();
        return snapshot with
        {
            Available = snapshot.Available is { } available
                ? available with { CanInstall = available.CanInstall && installer.IsInstalledByInstaller }
                : null,
            Install = installer.Status,
        };
    }
}
