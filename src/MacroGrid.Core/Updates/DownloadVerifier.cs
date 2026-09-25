using System.Security.Cryptography;

namespace MacroGrid.Core.Updates;

/// <summary>Checks a downloaded file against the SHA-256 the release reports. A mismatch means a broken or tampered download.</summary>
public static class DownloadVerifier
{
    public static async Task<bool> MatchesSha256Async(string path, string expectedHex, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var actual = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
        return string.Equals(actual, expectedHex.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
