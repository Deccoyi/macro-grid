using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MacroGrid.Core.Diagnostics;

/// <summary>What is known at run time about the person's PC, so the redactor can remove it from a text.</summary>
/// <param name="HomeFolder">The user profile folder, for example <c>C:\Users\Name</c>.</param>
/// <param name="DataFolder">The folder Macro Grid keeps its data in.</param>
/// <param name="UserName">The Windows user name.</param>
/// <param name="MachineName">The PC name.</param>
/// <param name="DeviceNames">The names of the paired devices.</param>
public sealed record RedactionContext(
    string? HomeFolder = null,
    string? DataFolder = null,
    string? UserName = null,
    string? MachineName = null,
    IReadOnlyList<string>? DeviceNames = null)
{
    /// <summary>The context of this PC: the real user profile folder, user name and PC name, with the given data folder and device names.</summary>
    public static RedactionContext ForThisPc(string dataDir, IEnumerable<string> deviceNames) => new(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), dataDir, Environment.UserName, Environment.MachineName, deviceNames.ToList());
}

/// <summary>
/// Removes personal details from one line of text before it is exported (the Error List export and the log export). Best effort: it
/// recognizes what it can and never promises more. The rules run in a fixed order: known folders, user and PC name, device names,
/// addresses, secrets, IP and e-mail addresses, long key-like runs. The same input always gives the same output. Every pattern is
/// linear-time (or has a match time limit) and a line is cut at <see cref="MaxLineLength"/> first, so no input makes it slow.
/// </summary>
public sealed partial class Redactor
{
    public const int MaxLineLength = 8000;
    private const int MinNameLength = 3;
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(250);

    private static readonly string[] SecretWords =
        ["password", "passwd", "pwd", "token", "secret", "apikey", "api_key", "authorization"];

    private readonly List<(Regex Pattern, string Replacement)> _known = [];

    public Redactor(RedactionContext context)
    {
        // The data folder usually sits inside the home folder, so it goes first to keep the more specific label.
        AddFolder(context.DataFolder, "<data>");
        AddFolder(context.HomeFolder, "<home>");
        AddWord(context.UserName, "<user>");
        AddWord(context.MachineName, "<pc>");

        var devices = (context.DeviceNames ?? [])
            .Select((name, i) => (Name: name?.Trim() ?? "", Label: $"<device {i + 1}>"))
            .Where(d => d.Name.Length >= 2)
            .OrderByDescending(d => d.Name.Length);
        foreach (var d in devices) AddWord(d.Name, d.Label, minLength: 2);
    }

    /// <summary>Returns the line with personal details replaced by short labels such as <c>&lt;home&gt;</c> or <c>&lt;redacted&gt;</c>.</summary>
    public string Line(string text)
    {
        if (string.IsNullOrEmpty(text)) return text ?? "";
        if (text.Length > MaxLineLength) text = string.Concat(text.AsSpan(0, MaxLineLength), "...");

        try
        {
            foreach (var (pattern, replacement) in _known) text = pattern.Replace(text, replacement);

            text = UserFolder().Replace(text, "<home>");
            text = UserInfo().Replace(text, "$1");
            text = QueryValue().Replace(text, "$1=<redacted>");
            text = Bearer().Replace(text, "Bearer <redacted>");
            text = NameValue().Replace(text, RedactNameValue);
            text = Ipv4().Replace(text, RedactIpv4);
            text = Ipv6().Replace(text, RedactIpv6);
            text = Email().Replace(text, "<email>");
            text = LongRun().Replace(text, m => $"<redacted:{m.Length.ToString(CultureInfo.InvariantCulture)}>");
            return text;
        }
        catch (RegexMatchTimeoutException)
        {
            return "<line removed: it could not be cleaned in time>";
        }
    }

    private void AddFolder(string? folder, string label)
    {
        folder = folder?.Trim().TrimEnd('\\', '/');
        if (string.IsNullOrEmpty(folder)) return;
        var parts = folder.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries).Select(Regex.Escape);
        var prefix = folder.StartsWith("\\\\", StringComparison.Ordinal) ? @"[\\/]{2}" : "";
        _known.Add((new Regex(prefix + string.Join(@"[\\/]+", parts), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, MatchTimeout), label));
    }

    private void AddWord(string? word, string label, int minLength = MinNameLength)
    {
        word = word?.Trim();
        if (string.IsNullOrEmpty(word) || word.Length < minLength) return;
        _known.Add((new Regex(@"(?<![\p{L}\p{N}_])" + Regex.Escape(word) + @"(?![\p{L}\p{N}_])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, MatchTimeout), label));
    }

    // name = value, name: value (also quoted values) where the name says it is a secret.
    private static string RedactNameValue(Match m)
    {
        var key = m.Groups["key"].Value;
        if (!IsSecretName(key)) return m.Value;
        var value = m.Groups["value"].Value;
        if (value is "<redacted>" || value.StartsWith("<redacted:", StringComparison.Ordinal)) return m.Value;
        return key + m.Groups["sep"].Value + "<redacted>";
    }

    private static bool IsSecretName(string key)
    {
        var lower = key.ToLowerInvariant();
        foreach (var word in SecretWords)
            if (lower.Contains(word, StringComparison.Ordinal)) return true;
        // "pin" is too short to look for inside a word ("mapping"): it counts only as a whole part of the name.
        return lower.Split(['_', '.', '-']).Contains("pin");
    }

    private static string RedactIpv4(Match m)
    {
        var parts = m.Value.Split('.');
        foreach (var p in parts)
            if (!int.TryParse(p, NumberStyles.None, CultureInfo.InvariantCulture, out var n) || n > 255) return m.Value;
        return parts[0] == "127" || m.Value == "0.0.0.0" ? m.Value : "<ip>";
    }

    private static string RedactIpv6(Match m) => m.Value is "::" or "::1" ? m.Value : "<ip>";

    [GeneratedRegex(@"\b[A-Za-z]:[\\/]Users[\\/][^\\/\s""'<>|]+", RegexOptions.IgnoreCase | RegexOptions.NonBacktracking | RegexOptions.CultureInvariant)]
    private static partial Regex UserFolder();

    [GeneratedRegex(@"(://)[^\s/@""']+@", RegexOptions.NonBacktracking | RegexOptions.CultureInvariant)]
    private static partial Regex UserInfo();

    [GeneratedRegex(@"([?&][^=&\s#""']+)=[^&\s#""']*", RegexOptions.NonBacktracking | RegexOptions.CultureInvariant)]
    private static partial Regex QueryValue();

    [GeneratedRegex(@"\bBearer\s+[A-Za-z0-9._~+/=-]+", RegexOptions.IgnoreCase | RegexOptions.NonBacktracking | RegexOptions.CultureInvariant)]
    private static partial Regex Bearer();

    [GeneratedRegex(@"(?<key>[A-Za-z0-9_.\-]+)(?<sep>\s*[:=]\s*)(?<value>""[^""]*""|'[^']*'|[^\s,;]+)", RegexOptions.NonBacktracking | RegexOptions.CultureInvariant)]
    private static partial Regex NameValue();

    [GeneratedRegex(@"\b\d{1,3}(?:\.\d{1,3}){3}\b", RegexOptions.NonBacktracking | RegexOptions.CultureInvariant)]
    private static partial Regex Ipv4();

    [GeneratedRegex(
        @"(?:\b(?:[0-9a-fA-F]{1,4}:){7}[0-9a-fA-F]{1,4}\b|\b(?:[0-9a-fA-F]{1,4}:){1,7}:(?:[0-9a-fA-F]{1,4}(?::[0-9a-fA-F]{1,4}){0,6}\b)?|::[0-9a-fA-F]{1,4}(?::[0-9a-fA-F]{1,4}){0,6}\b)",
        RegexOptions.NonBacktracking | RegexOptions.CultureInvariant)]
    private static partial Regex Ipv6();

    [GeneratedRegex(@"\b[\w.+-]+@[\w-]+(?:\.[\w-]+)+\b", RegexOptions.NonBacktracking | RegexOptions.CultureInvariant)]
    private static partial Regex Email();

    [GeneratedRegex(@"[A-Za-z0-9+/=_-]{32,}", RegexOptions.NonBacktracking | RegexOptions.CultureInvariant)]
    private static partial Regex LongRun();
}
