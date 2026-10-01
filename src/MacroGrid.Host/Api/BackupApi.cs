using MacroGrid.Core.Backup;
using MacroGrid.Host.Ui;
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

        // A full backup the person saves wherever they like.
        api.MapPost("/backup/export", async (IUiDialogService dialogs, BackupService backups) =>
        {
            byte[] bytes;
            try { bytes = await Task.Run(backups.BuildBackup); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return ApiResults.BadRequest("The backup could not be made."); }
            var path = await dialogs.SaveFileAsync("Back up everything", $"macro-grid-{DateTime.Now:yyyyMMdd-HHmm}{BackupFile.Extension}",
                "Macro Grid backup (*.mgbackup)|*.mgbackup", "mgbackup", bytes);
            return Results.Json(new { path });
        });

        // Opens a backup file and answers what differs from what is here; nothing changes yet.
        api.MapPost("/backup/inspect", async (IUiDialogService dialogs, BackupRestorer restorer) =>
        {
            var (path, bytes) = await dialogs.OpenFileAsync("Restore from a backup", "Macro Grid backup (*.mgbackup)|*.mgbackup");
            if (bytes is null) return Results.Json(new { path = (string?)null });
            try { return ApiResults.Json(new { path, result = await Task.Run(() => restorer.Inspect(bytes)) }); }
            catch (InvalidDataException ex) { return ApiResults.BadRequest(ex.Message); }
        });

        // Applies the ticked items of the backup that was inspected; a restore point is made first.
        api.MapPost("/backup/restore", async (HttpRequest request, BackupRestorer restorer) =>
        {
            var (valid, body) = await ApiResults.ReadJsonAsync<RestoreRequest>(request);
            if (!valid || body is null || string.IsNullOrEmpty(body.Id) || body.Items is null) return ApiResults.InvalidJson();
            try { return ApiResults.Json(await restorer.RestoreAsync(body.Id, body.Items)); }
            catch (RestoreSessionException ex) { return ApiResults.BadRequest(ex.Message); }
            catch (BackupException) { return ApiResults.BadRequest("A restore point could not be made, so nothing was restored."); }
        });

        return api;
    }

    private sealed record RestorePointRequest(string? Reason);

    private sealed record RestoreRequest(string? Id, List<RestoreRequestItem>? Items);
}
