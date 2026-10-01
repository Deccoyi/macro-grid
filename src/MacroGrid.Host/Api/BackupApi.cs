using MacroGrid.Core.Backup;
using Microsoft.AspNetCore.Routing;

namespace MacroGrid.Host.Api;

/// <summary>Editor endpoints for backups and restore points (see docs/design/backup-restore.md).</summary>
internal static class BackupApi
{
    public static RouteGroupBuilder MapBackupApi(this RouteGroupBuilder api)
    {
        api.MapGet("/restore-points", (BackupService backups) => ApiResults.Json(backups.ListRestorePoints()));

        // The editor asks for a point before it overwrites a profile by an import; the other reasons are made by the server itself.
        api.MapPost("/restore-points", async (HttpRequest request, BackupService backups) =>
        {
            var (valid, body) = await ApiResults.ReadJsonAsync<RestorePointRequest>(request);
            if (!valid || body is null) return ApiResults.InvalidJson();
            if (body.Reason != BackupService.ReasonImport) return ApiResults.BadRequest("Unknown restore point reason.");
            try
            {
                await Task.Run(() => backups.CreateRestorePoint(body.Reason));
                return Results.NoContent();
            }
            catch (BackupException ex)
            {
                return ApiResults.BadRequest(ex.Message);
            }
        });

        return api;
    }

    private sealed record RestorePointRequest(string? Reason);
}
