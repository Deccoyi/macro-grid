using System.Text.RegularExpressions;

namespace MacroGrid.Core.Diagnostics;

/// <summary>
/// The crash report's own pass, run after <see cref="Redactor"/> (so <c>&lt;home&gt;</c> and <c>&lt;data&gt;</c> are already in place):
/// a path under the install folder becomes <c>&lt;app&gt;\...</c>; a path under <c>&lt;home&gt;</c> or <c>&lt;data&gt;</c> (except the log folder)
/// and any other absolute path becomes <c>&lt;path&gt;</c>; the host of a URL and a <c>host.name:port</c> pair become <c>&lt;host&gt;</c> (a URL
/// loses its path); a MAC address becomes <c>&lt;mac&gt;</c>. The redactor leaves all of this alone on purpose, but a crash report promises that
/// no file path outside the app's own folders and no network name is shown. Best effort: a file name with spaces and no extension can leave
/// its tail behind. Every pattern is linear-time.
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

        line = LabelledPath().Replace(line, m =>
            m.Groups["label"].Value == "data" && LogsFolder().IsMatch(m.Groups["rest"].Value) && !ParentSegment().IsMatch(m.Groups["rest"].Value) ? m.Value : "<path>");
        line = DrivePath().Replace(line, "<path>");
        line = UncPath().Replace(line, "${before}<path>");
        line = Url().Replace(line, m => IsLocal(m.Groups["host"].Value) ? m.Value : m.Groups["scheme"].Value + "<host>");
        line = HostPort().Replace(line, "<host>");
        return Mac().Replace(line, "<mac>");
    }

    // Exact match after the port is cut off, so "localhost.evil.lan" and "127.0.0.1.nip.io" do not count as local.
    private static bool IsLocal(string host)
    {
        var end = host.StartsWith('[') ? host.IndexOf(']') + 1 : host.LastIndexOf(':');
        var name = end > 0 ? host[..end] : host;
        return name.Equals("localhost", StringComparison.OrdinalIgnoreCase) || name == "[::1]" || Loopback4().IsMatch(name);
    }

    [GeneratedRegex(@"^127(?:\.\d{1,3}){3}$", Linear)]
    private static partial Regex Loopback4();

    // A ".." segment lets a path climb out of the log folder.
    [GeneratedRegex(@"(?:^|[\\/])\.\.(?:[\\/]|$)", Linear)]
    private static partial Regex ParentSegment();

    // Folder names may hold spaces; the last part ends at the first whitespace, quote, angle bracket, pipe or colon (so ":line 42" stays).
    private const string Rest = @"(?:[^\\/\r\n""'<>|:]+[\\/])*[^\s\\/""'<>|:]*";

    [GeneratedRegex(@"\b[A-Za-z]:[\\/]" + Rest, Linear)]
    private static partial Regex DrivePath();

    // A label such as <data>\\x must not look like a share, so a share starts at the line start or after a separator character.
    [GeneratedRegex(@"(?<before>^|[\s""'(\[=,;])\\\\" + Rest, Linear)]
    private static partial Regex UncPath();

    // <home>\ or <data>\ and what follows. The last part may hold spaces when it ends in an extension ("Tax 2026.pdf"); otherwise it ends at the first space.
    [GeneratedRegex(@"<(?<label>home|data)>[\\/]+(?<rest>(?:[^\\/\r\n""'<>|:]+[\\/])*(?:[^\\/\r\n""'<>|:]*?\.[A-Za-z0-9]{1,8}\b|[^\s\\/""'<>|:]*))", Linear)]
    private static partial Regex LabelledPath();

    // The log folder is the one place under <data> a report may name.
    [GeneratedRegex(@"^logs[\\/]", Linear | RegexOptions.IgnoreCase)]
    private static partial Regex LogsFolder();

    // scheme://host[:port] and an optional path, query or fragment (the path can carry a profile or file name).
    [GeneratedRegex(@"(?<scheme>\b[A-Za-z][A-Za-z0-9+.-]*://)(?<host>[^\s/?#""'@]+)(?:[/?#][^\s""']*)?", Linear)]
    private static partial Regex Url();

    // A dotted name with a port, as in "No such host is known. (nas.home.lan:443)"; it needs a letter in the last label so "12.30:45" stays.
    [GeneratedRegex(@"\b(?:[A-Za-z0-9-]+\.)+[A-Za-z][A-Za-z0-9-]*:\d{1,5}\b", Linear)]
    private static partial Regex HostPort();

    [GeneratedRegex(@"\b[0-9A-Fa-f]{2}(?:[:-][0-9A-Fa-f]{2}){5}\b", Linear)]
    private static partial Regex Mac();
}
