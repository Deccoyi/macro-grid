using System.Globalization;
using System.Text.RegularExpressions;

namespace MacroGrid.Core.Updates;

/// <summary>
/// A version with the SemVer pre-release label, so <c>0.2.1 &lt; 0.3.0-alpha &lt; 0.3.0</c>. Unlike
/// <see cref="Plugins.SemVer"/> (three numbers, for plugin compatibility) this one orders releases for the updater.
/// Build metadata after <c>+</c> is accepted and ignored.
/// </summary>
public readonly partial record struct ReleaseVersion(int Major, int Minor, int Patch, string? PreRelease = null) : IComparable<ReleaseVersion>
{
    [GeneratedRegex(@"^v?(\d+)\.(\d+)\.(\d+)(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+[0-9A-Za-z.-]+)?$")]
    private static partial Regex Pattern();

    /// <summary>Parses "0.3.0", "v0.3.0" or "0.3.0-alpha.2".</summary>
    public static bool TryParse(string? text, out ReleaseVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var match = Pattern().Match(text.Trim());
        if (!match.Success) return false;
        if (!int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var major)
            || !int.TryParse(match.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var minor)
            || !int.TryParse(match.Groups[3].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var patch))
            return false;
        version = new ReleaseVersion(major, minor, patch, match.Groups[4].Success ? match.Groups[4].Value : null);
        return true;
    }

    /// <summary>Parses a release tag such as "server-v0.3.0-alpha" after removing <paramref name="prefix"/> ("server-v"). Other tags do not parse.</summary>
    public static bool TryParseTag(string? tag, string prefix, out ReleaseVersion version)
    {
        version = default;
        if (tag is null || !tag.StartsWith(prefix, StringComparison.Ordinal)) return false;
        return TryParse(tag[prefix.Length..], out version);
    }

    public bool IsPreRelease => PreRelease is not null;

    /// <summary>"0.3.0", the part installer and asset file names use (no label).</summary>
    public string Core => $"{Major}.{Minor}.{Patch}";

    public override string ToString() => PreRelease is null ? Core : $"{Core}-{PreRelease}";

    public int CompareTo(ReleaseVersion other)
    {
        if (Major != other.Major) return Major.CompareTo(other.Major);
        if (Minor != other.Minor) return Minor.CompareTo(other.Minor);
        if (Patch != other.Patch) return Patch.CompareTo(other.Patch);
        return ComparePreRelease(PreRelease, other.PreRelease);
    }

    public static bool operator <(ReleaseVersion a, ReleaseVersion b) => a.CompareTo(b) < 0;
    public static bool operator >(ReleaseVersion a, ReleaseVersion b) => a.CompareTo(b) > 0;
    public static bool operator <=(ReleaseVersion a, ReleaseVersion b) => a.CompareTo(b) <= 0;
    public static bool operator >=(ReleaseVersion a, ReleaseVersion b) => a.CompareTo(b) >= 0;

    /// <summary>A release outranks any pre-release of the same numbers; labels compare identifier by identifier (numbers numerically, before text).</summary>
    private static int ComparePreRelease(string? a, string? b)
    {
        if (a is null && b is null) return 0;
        if (a is null) return 1;
        if (b is null) return -1;

        var left = a.Split('.');
        var right = b.Split('.');
        for (var i = 0; i < Math.Min(left.Length, right.Length); i++)
        {
            var leftIsNumber = long.TryParse(left[i], NumberStyles.None, CultureInfo.InvariantCulture, out var leftNumber);
            var rightIsNumber = long.TryParse(right[i], NumberStyles.None, CultureInfo.InvariantCulture, out var rightNumber);
            int result;
            if (leftIsNumber && rightIsNumber) result = leftNumber.CompareTo(rightNumber);
            else if (leftIsNumber) result = -1;
            else if (rightIsNumber) result = 1;
            else result = string.CompareOrdinal(left[i], right[i]);
            if (result != 0) return result;
        }
        return left.Length.CompareTo(right.Length);
    }
}
