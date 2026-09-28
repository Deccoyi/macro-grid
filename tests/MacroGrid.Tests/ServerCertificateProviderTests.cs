using System.Text.RegularExpressions;
using MacroGrid.Core.Security;

namespace MacroGrid.Tests;

public sealed partial class ServerCertificateProviderTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ms-cert-" + Guid.NewGuid().ToString("N"));

    public ServerCertificateProviderTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [GeneratedRegex("^[0-9a-f]{64}$")]
    private static partial Regex LowercaseSha256Hex();

    /// <summary>Reversible and obviously not the plain bytes, so a test can see the round trip actually went through this.</summary>
    private sealed class TestProtector(string key = "k1") : ISecretProtector
    {
        public string Protect(string secret) => $"{key}:" + new string(secret.Reverse().ToArray());
        public string? Unprotect(string protectedSecret) =>
            protectedSecret.StartsWith($"{key}:", StringComparison.Ordinal) ? new string(protectedSecret[(key.Length + 1)..].Reverse().ToArray()) : null;
    }

    [Fact]
    public void Fingerprint_is_lowercase_sha256_hex()
    {
        var provider = new ServerCertificateProvider(_dir, new TestProtector());
        Assert.Matches(LowercaseSha256Hex(), provider.Fingerprint);
    }

    [Fact]
    public void The_same_data_folder_reloads_the_same_certificate()
    {
        var first = new ServerCertificateProvider(_dir, new TestProtector());
        var second = new ServerCertificateProvider(_dir, new TestProtector());

        Assert.Equal(first.Fingerprint, second.Fingerprint);
        Assert.Equal(first.Certificate.Thumbprint, second.Certificate.Thumbprint);
    }

    [Fact]
    public void A_file_a_different_protector_cannot_read_gets_a_fresh_certificate_instead_of_throwing()
    {
        var first = new ServerCertificateProvider(_dir, new TestProtector("k1"));
        var second = new ServerCertificateProvider(_dir, new TestProtector("k2"));

        Assert.NotEqual(first.Fingerprint, second.Fingerprint);
    }

    [Fact]
    public void The_certificate_has_a_usable_private_key()
    {
        var provider = new ServerCertificateProvider(_dir, new TestProtector());
        Assert.True(provider.Certificate.HasPrivateKey);
    }
}
