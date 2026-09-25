using System.IO.Compression;

namespace MacroGrid.Core.Plugins.Distribution;

/// <summary>
/// Safely unzips a downloaded plugin package into a staging folder: entries with <c>..</c>, an absolute path, or a
/// resolved path outside the destination are rejected, and each entry plus the archive total is capped (the same
/// spirit as MacroGrid.Core.Profiles.ProfilePackage's entry reading, extended to a whole-folder extract).
/// </summary>
public static class PluginZip
{
    private const long MaxEntryBytes = 100L * 1024 * 1024;
    private const long MaxTotalBytes = 150L * 1024 * 1024;

    /// <exception cref="PluginDownloadException">The archive is not safe to extract.</exception>
    public static void ExtractSafely(byte[] zipBytes, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);
        var destinationFull = Path.GetFullPath(destinationDir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        using var zip = new ZipArchive(new MemoryStream(zipBytes), ZipArchiveMode.Read);
        long total = 0;
        foreach (var entry in zip.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name)) continue; // a directory entry

            if (Path.IsPathRooted(entry.FullName) || entry.FullName.Contains(".."))
                throw new PluginDownloadException(PluginDownloadException.Verify, $"The package has an unsafe path: {entry.FullName}");

            var targetFull = Path.GetFullPath(Path.Combine(destinationDir, entry.FullName));
            if (!targetFull.StartsWith(destinationFull, StringComparison.OrdinalIgnoreCase))
                throw new PluginDownloadException(PluginDownloadException.Verify, $"The package has an unsafe path: {entry.FullName}");

            if (entry.Length > MaxEntryBytes)
                throw new PluginDownloadException(PluginDownloadException.Verify, "The package contains a file that is too large.");
            total += entry.Length;
            if (total > MaxTotalBytes)
                throw new PluginDownloadException(PluginDownloadException.Verify, "The package is too large once extracted.");

            Directory.CreateDirectory(Path.GetDirectoryName(targetFull)!);
            using var source = entry.Open();
            using var destination = new FileStream(targetFull, FileMode.Create, FileAccess.Write, FileShare.None);
            source.CopyTo(destination);
        }
    }
}
