using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace MacroGrid.PluginTool;

public static partial class PluginToolApp
{
    private static readonly DateTimeOffset FixedTimestamp = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>The same bytes for the same folder: sorted entries with "/" names, one fixed timestamp. Only the same tool version promises it, since
    /// the compressor belongs to the runtime.</summary>
    private static int Pack(string[] args, TextWriter output, TextWriter error)
    {
        if (!TryParse(args, ["--out"], [], out var folders, out var options, out var problem) || folders.Count != 1)
            return UsageError(problem ?? "pack takes one folder", error);

        var folder = folders[0];
        var report = Check(folder, output);
        if (report.HasErrors) return Failed;

        var manifest = report.Manifest!;
        var outDir = options.TryGetValue("--out", out var o) && o.Count > 0 ? o[0] : Directory.GetCurrentDirectory();
        var zipPath = Path.Combine(outDir, $"{manifest.Id}-{manifest.Version}.zip");

        var root = Path.GetFullPath(folder);
        var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(f => (Full: f, Name: Path.GetRelativePath(root, f).Replace(Path.DirectorySeparatorChar, '/')))
            .Where(f => !IsLeftOut(f.Name))
            .OrderBy(f => f.Name, StringComparer.Ordinal)
            .ToList();

        Directory.CreateDirectory(outDir);
        using (var stream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            foreach (var (full, name) in files)
            {
                var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
                entry.LastWriteTime = FixedTimestamp;
                using var target = entry.Open();
                using var source = File.OpenRead(full);
                source.CopyTo(target);
            }
        }

        var sha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(zipPath)));
        File.WriteAllText(zipPath + ".sha256", sha256, new UTF8Encoding(false));
        output.WriteLine($"wrote {zipPath}");
        output.WriteLine($"sha256={sha256}");
        output.WriteLine($"size={new FileInfo(zipPath).Length}");
        return Ok;
    }

    /// <summary>The plugin's own saved data and anything hidden (a .git folder, an editor's leftovers) never go into a package.</summary>
    private static bool IsLeftOut(string relativeName) =>
        relativeName is "settings.json" or "storage.json" || relativeName.Split('/').Any(part => part.StartsWith('.'));

    /// <summary>Splits the arguments into plain ones and the named options. A name in <paramref name="valued"/> takes one value, a name in
    /// <paramref name="repeatable"/> takes one value and may come again.</summary>
    private static bool TryParse(string[] args, string[] valued, string[] repeatable, out List<string> plain,
        out Dictionary<string, List<string>> options, out string? problem)
    {
        plain = [];
        options = [];
        problem = null;
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (!arg.StartsWith("--")) { plain.Add(arg); continue; }

            var isRepeatable = repeatable.Contains(arg);
            if (!isRepeatable && !valued.Contains(arg)) { problem = $"Unknown option '{arg}'"; return false; }
            if (i + 1 >= args.Length) { problem = $"{arg} needs a value"; return false; }
            if (!options.TryGetValue(arg, out var list)) options[arg] = list = [];
            if (list.Count > 0 && !isRepeatable) { problem = $"{arg} was given twice"; return false; }
            list.Add(args[++i]);
        }
        return true;
    }
}
