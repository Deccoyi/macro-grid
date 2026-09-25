using System.Text.RegularExpressions;

namespace MacroGrid.Core.Plugins;

/// <summary>
/// Minimal MAJOR.MINOR.PATCH comparison for plugin compatibility checks — not a full SemVer
/// implementation (a pre-release label or build metadata after the three numbers is ignored), which is all
/// docs/guides/versioning.md's scheme needs.
/// </summary>
public static partial class SemVer
{
    [GeneratedRegex(@"^(\d+)\.(\d+)\.(\d+)")]
    private static partial Regex VersionPattern();

    [GeneratedRegex(@"^\d+\.\d+\.\d+$")]
    private static partial Regex ThreePartPattern();

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

    /// <summary>True for exactly MAJOR.MINOR.PATCH (for example "1.3.0"): no label, no missing part.</summary>
    public static bool IsThreePartVersion(string? version) =>
        version is not null && ThreePartPattern().IsMatch(version);

    /// <summary>The rule a plugin's <c>macroGrid</c> field follows: <paramref name="required"/> is the oldest Macro Grid the
    /// plugin runs on, and it runs on every later version of the same MAJOR. So "1.3.0" runs on 1.3.0 up to (excluding)
    /// 2.0.0. A label such as "-beta" on <paramref name="actual"/> is ignored.</summary>
    public static bool SatisfiesPlatform(string actual, string required)
    {
        if (!TryParse(actual, out var a) || !IsThreePartVersion(required) || !TryParse(required, out var r)) return false;
        return a.Major == r.Major && Compare(a, r) >= 0;
    }
}
