using System.IO.Compression;
using System.Text.Json;
using MacroGrid.Protocol;
using Microsoft.Extensions.Logging;

namespace MacroGrid.Core.Backup;

/// <summary>A restore point could not be made or read; the message is safe to show.</summary>
public sealed class BackupException(string message, Exception? inner = null) : Exception(message, inner);

public sealed record RestorePointInfo(string FileName, DateTimeOffset CreatedAt, string Reason, long Size);

/// <summary>
/// Makes full backups and keeps the automatic restore points (a restore point is a backup file in <c>restore-points/</c> of the data folder, made before
/// something is overwritten or deleted). One lock covers making a point and applying a restore. If a point cannot be written the caller must not
/// go on with the action that asked for it.
/// </summary>
public sealed partial class BackupService(BackupCollector collector, string dataDir, string serverVersion, ILogger? log = null)
{
    public const string FolderName = "restore-points";
    public const int KeepNewest = 10;
    public const int KeepAtLeast = 2;
    public static readonly TimeSpan MaxAge = TimeSpan.FromDays(30);
    public const long MaxTotalBytes = 500L * 1024 * 1024;

    /// <summary>The reasons a restore point can have; the editor may ask only for <see cref="ReasonImport"/>.</summary>
    public const string ReasonRestore = "restore";
    public const string ReasonImport = "import";
    public const string ReasonDelete = "delete";
    public const string ReasonUpdate = "update";

    private static readonly string[] Reasons = [ReasonRestore, ReasonImport, ReasonDelete, ReasonUpdate];
    private static readonly JsonSerializerOptions Json = new(ProtocolJson.Options);

    private readonly Lock _lock = new();
    private readonly string _folder = Path.Combine(dataDir, FolderName);

    /// <summary>Lists the files of the folder; tests replace it to make the listing fail.</summary>
    internal Func<string, string, IEnumerable<string>> EnumerateFiles { get; set; } = Directory.EnumerateFiles;

    public static bool IsKnownReason(string? reason) => reason is not null && Reasons.Contains(reason);

    /// <summary>A full backup of what is here now, for the person to save.</summary>
    public byte[] BuildBackup()
    {
        lock (_lock) return Build(BackupFile.KindBackup, "manual", Collect());
    }

    private BackupContent Collect()
    {
        var content = collector.Collect();
        foreach (var warning in content.Warnings) log?.LogWarning("Backup: {Warning}", warning);
        return content;
    }

    /// <summary>Makes a restore point of what is here now, unless the newest one already holds exactly this data.</summary>
    /// <exception cref="BackupException">The point could not be written.</exception>
    public void CreateRestorePoint(string reason)
    {
        lock (_lock) CreateRestorePointLocked(reason);
    }

    /// <summary>After a start with a different version than the last run: one restore point, never a reason to stop the start.</summary>
    public bool TryCreateUpdatePoint(string? lastRunVersion, string currentVersion)
    {
        if (string.IsNullOrEmpty(lastRunVersion) || string.Equals(lastRunVersion, currentVersion, StringComparison.Ordinal)) return false;
        try
        {
            CreateRestorePoint(ReasonUpdate);
            return true;
        }
        catch (Exception ex)
        {
            // Any failure, not only BackupException: this runs at start-up, and LastRunVersion is saved after it.
            log?.LogWarning(ex, "No restore point was made after the update: {Message}", ex.Message);
            return false;
        }
    }

    /// <summary>Applies the retention rules (count, size, age). Runs at start and after each new point; a folder that cannot be cleaned is left as it is.</summary>
    /// <param name="now">The time the age is measured against; tests pass their own.</param>
    public void Prune(DateTimeOffset? now = null)
    {
        try { lock (_lock) PruneLocked(now ?? DateTimeOffset.UtcNow); }
        catch (Exception ex)
        {
            log?.LogWarning(ex, "Old restore points could not be cleaned up: {Message}", ex.Message);
        }
    }

    public IReadOnlyList<RestorePointInfo> ListRestorePoints()
    {
        lock (_lock) return [.. Points().Select(p => p.Info)];
    }

    /// <summary>The bytes of a listed restore point. Only a name from the list is accepted, never a path.</summary>
    /// <exception cref="BackupException">The name is not one of the restore points.</exception>
    public byte[] ReadRestorePoint(string fileName)
    {
        lock (_lock)
        {
            var point = Points().FirstOrDefault(p => string.Equals(p.Info.FileName, fileName, StringComparison.Ordinal))
                ?? throw new BackupException("That restore point does not exist.");
            try { return File.ReadAllBytes(point.Path); }
            catch (IOException ex) { throw new BackupException("The restore point could not be read.", ex); }
        }
    }

    private void CreateRestorePointLocked(string reason)
    {
        if (!IsKnownReason(reason)) throw new BackupException("Unknown restore point reason.");
        try
        {
            var content = Collect();
            var hash = BackupFile.HashOf(content);
            var points = Points();
            if (points.Count > 0 && points[0].Hash == hash) return;

            var bytes = Build(BackupFile.KindRestorePoint, reason, content);
            Directory.CreateDirectory(_folder);
            var stamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss");
            var path = Path.Combine(_folder, $"{stamp}-{reason}{BackupFile.Extension}");
            for (var n = 2; File.Exists(path); n++) path = Path.Combine(_folder, $"{stamp}-{reason}-{n}{BackupFile.Extension}");

            var tmp = path + ".tmp";
            File.WriteAllBytes(tmp, bytes);
            File.Move(tmp, path);
        }
        catch (Exception ex) when (ex is not BackupException)
        {
            // Anything the collecting threw (a damaged store, a plugin) refuses the action that wanted the point, like a full disk does.
            throw new BackupException("A restore point could not be made.", ex);
        }
        // The point is written; a failed clean-up must never refuse the action that asked for it.
        Prune();
    }

    private byte[] Build(string kind, string reason, BackupContent content) =>
        BackupFile.Write(content, kind, reason, serverVersion, collector.RequiredPlugins(content.Profiles));

    private sealed record Point(RestorePointInfo Info, string Path, string Hash);

    /// <summary>The restore points, newest first by the time inside the file (a clock set back must not reorder them by name).</summary>
    /// <param name="damaged">Receives the files that are not a readable backup at all (a broken zip or manifest); a file that is merely locked or is another kind of backup is not one of them.</param>
    private List<Point> Points(List<string>? damaged = null)
    {
        var found = new List<Point>();
        if (!Directory.Exists(_folder)) return found;
        foreach (var file in EnumerateFiles(_folder, "*" + BackupFile.Extension))
        {
            try
            {
                using var zip = ZipFile.OpenRead(file);
                var entry = zip.GetEntry("manifest.json");
                if (entry is null || entry.Length > 1024 * 1024) { damaged?.Add(file); continue; }
                using var stream = entry.Open();
                var manifest = JsonSerializer.Deserialize<BackupManifest>(stream, Json);
                if (manifest is null) { damaged?.Add(file); continue; }
                if (manifest.Kind != BackupFile.KindRestorePoint) continue;
                found.Add(new Point(new RestorePointInfo(Path.GetFileName(file), manifest.CreatedAt, manifest.Reason, new FileInfo(file).Length), file, manifest.ContentHash));
            }
            catch (Exception ex) when (ex is InvalidDataException or JsonException) { damaged?.Add(file); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* locked or unreadable right now; left alone */ }
        }
        return [.. found.OrderByDescending(p => p.Info.CreatedAt).ThenByDescending(p => p.Info.FileName, StringComparer.Ordinal)];
    }

    private void PruneLocked(DateTimeOffset now)
    {
        var damaged = new List<string>();
        var points = Points(damaged);
        // A damaged file is no restore point and cannot be ordered by its manifest, so it goes by the age of the file.
        foreach (var file in damaged)
        {
            try
            {
                if (now - File.GetLastWriteTimeUtc(file) <= MaxAge) continue;
                log?.LogWarning("Removing a damaged restore point file older than {Days} days: {File}", MaxAge.TotalDays, Path.GetFileName(file));
                File.Delete(file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* left for the next run */ }
        }
        var keep = points.Count;
        long total = points.Sum(p => p.Info.Size);
        // Beyond the newest ten, over the size cap or older than 30 days go first (oldest first), but never below the newest two.
        while (keep > KeepAtLeast && (keep > KeepNewest || total > MaxTotalBytes || now - points[keep - 1].Info.CreatedAt > MaxAge))
        {
            var oldest = points[keep - 1];
            try { File.Delete(oldest.Path); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { break; }
            total -= oldest.Info.Size;
            keep--;
        }
    }
}
