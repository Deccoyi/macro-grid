using System.Text.RegularExpressions;

namespace MacroGrid.Core.Plugins;

/// <summary>
/// Minimal MAJOR.MINOR.PATCH comparison for plugin compatibility checks — not a full SemVer
/// implementation (no pre-release/build metadata), which is all docs/guides/versioning.md's scheme needs.
/// </summary>
public static partial class SemVer
{
    [GeneratedRegex(@"^(\d+)\.(\d+)\.(\d+)")]
    private static partial Regex VersionPattern();

    public static bool TryParse(string version, out (int Major, int Minor, int Patch) parsed)
    {
        var match = VersionPattern().Match(version);
        if (!match.Success)
        {
            parsed = default;
            return false;
        }

        parsed = (int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value), int.Parse(match.Groups[3].Value));
        return true;
    }

    public static int Compare((int Major, int Minor, int Patch) a, (int Major, int Minor, int Patch) b)
    {
        if (a.Major != b.Major) return a.Major.CompareTo(b.Major);
        if (a.Minor != b.Minor) return a.Minor.CompareTo(b.Minor);
        return a.Patch.CompareTo(b.Patch);
    }

    /// <summary>Compares two version strings; an unparsable one sorts before every parsable one.</summary>
    public static int CompareVersionStrings(string a, string b)
    {
        var aOk = TryParse(a, out var pa);
        var bOk = TryParse(b, out var pb);
        if (!aOk || !bOk) return aOk.CompareTo(bOk);
        return Compare(pa, pb);
    }

    /// <summary>Is <paramref name="actual"/> &gt;= <paramref name="minimum"/>?</summary>
    public static bool SatisfiesMinimum(string actual, string minimum)
    {
        if (!TryParse(actual, out var a) || !TryParse(minimum, out var m)) return false;
        return Compare(a, m) >= 0;
    }

    /// <summary>npm-style caret range: "^1.2.3" matches 1.2.3 up to (excluding) 2.0.0, except for 0.x.y
    /// where it matches 0.x.z (patch-level only, per npm's 0.x caret rule and this project's SemVer policy).</summary>
    public static bool SatisfiesCaret(string actual, string range)
    {
        var trimmed = range.TrimStart('^');
        if (!TryParse(actual, out var a) || !TryParse(trimmed, out var r)) return false;

        if (Compare(a, r) < 0) return false;

        return r.Major > 0 ? a.Major == r.Major : a.Minor == r.Minor;
    }
}
