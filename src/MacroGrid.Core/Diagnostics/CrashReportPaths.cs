using System.Text.RegularExpressions;

namespace MacroGrid.Core.Diagnostics;

/// <summary>
/// The crash report's own path pass, run after <see cref="Redactor"/> (so <c>&lt;home&gt;</c> and <c>&lt;data&gt;</c> are already in place):
/// a path under the install folder becomes <c>&lt;app&gt;\...</c>, any other absolute path becomes <c>&lt;path&gt;</c>. The redactor leaves such
/// paths alone on purpose, but a crash report promises that no file path outside the app's own folders is shown. Best effort: a file name
/// with spaces can leave its tail behind. Every pattern is linear-time.
/// </summary>
public static partial class CrashReportPaths
{
    private const RegexOptions Linear = RegexOptions.NonBacktracking | RegexOptions.CultureInvariant;

    public static string Clean(string line, string installDir)
    {
        if (string.IsNullOrEmpty(line)) return line ?? "";

        var install = installDir?.Trim().TrimEnd('\\', '/');
        if (!string.IsNullOrEmpty(install))
        {
            var parts = install.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries).Select(Regex.Escape);
            var prefix = install.StartsWith("\\\\", StringComparison.Ordinal) ? @"[\\/]{2}" : "";
            line = new Regex(prefix + string.Join(@"[\\/]+", parts) + @"[\\/]+", Linear | RegexOptions.IgnoreCase).Replace(line, @"<app>\");
        }

        line = DrivePath().Replace(line, "<path>");
        return UncPath().Replace(line, "${before}<path>");
    }

    // Folder names may hold spaces; the last part ends at the first whitespace, quote, angle bracket, pipe or colon (so ":line 42" stays).
    private const string Rest = @"(?:[^\\/\r\n""'<>|:]+[\\/])*[^\s\\/""'<>|:]*";

    [GeneratedRegex(@"\b[A-Za-z]:[\\/]" + Rest, Linear)]
    private static partial Regex DrivePath();

    // A label such as <data>\\x must not look like a share, so a share starts at the line start or after a separator character.
    [GeneratedRegex(@"(?<before>^|[\s""'(\[=,;])\\\\" + Rest, Linear)]
    private static partial Regex UncPath();
}
