using System.Security.Cryptography;
using MacroGrid.Core.Updates;

namespace MacroGrid.Tests;

public sealed class DownloadVerifierTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ms-tests-" + Guid.NewGuid().ToString("N"));

    public DownloadVerifierTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string WriteFile(byte[] content)
    {
        var path = Path.Combine(_dir, "installer.exe");
        File.WriteAllBytes(path, content);
        return path;
    }

    [Fact]
    public async Task Matching_digest_passes_in_any_letter_case()
    {
        var content = "installer bytes"u8.ToArray();
        var hex = Convert.ToHexString(SHA256.HashData(content));
        var path = WriteFile(content);

        Assert.True(await DownloadVerifier.MatchesSha256Async(path, hex.ToLowerInvariant(), CancellationToken.None));
        Assert.True(await DownloadVerifier.MatchesSha256Async(path, hex.ToUpperInvariant(), CancellationToken.None));
    }

    [Fact]
    public async Task Mismatching_digest_fails()
    {
        var path = WriteFile("tampered"u8.ToArray());
        var hexOfOther = Convert.ToHexString(SHA256.HashData("installer bytes"u8.ToArray()));

        Assert.False(await DownloadVerifier.MatchesSha256Async(path, hexOfOther, CancellationToken.None));
        Assert.False(await DownloadVerifier.MatchesSha256Async(path, "", CancellationToken.None));
    }
}
