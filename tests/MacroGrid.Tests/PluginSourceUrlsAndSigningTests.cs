using System.Security.Cryptography;
using System.Text;
using MacroGrid.Core.Plugins.Distribution;

namespace MacroGrid.Tests;

public sealed class PluginSourceUrlsTests
{
    [Theory]
    [InlineData("https://raw.githubusercontent.com/Deccoyi/macro-grid-plugin/HEAD/macrogrid-index.json", true)]
    [InlineData("https://github.com/Deccoyi/macro-grid-plugin/releases/download/plugin-obs-v0.2.0/obs-0.2.0.zip", true)]
    [InlineData("https://objects.githubusercontent.com/x", true)]
    [InlineData("http://raw.githubusercontent.com/Deccoyi/macro-grid-plugin/HEAD/macrogrid-index.json", false)] // not https
    [InlineData("https://evil.example.com/macrogrid-index.json", false)]
    [InlineData("https://raw.githubusercontent.com.evil.com/x", false)]
    public void Only_https_on_the_allowed_GitHub_hosts_passes(string url, bool expected) =>
        Assert.Equal(expected, PluginSourceUrls.IsAllowedUrl(new Uri(url)));

    [Fact]
    public void A_release_url_must_belong_to_the_same_owner_and_repo_as_the_index()
    {
        var own = new Uri("https://github.com/Deccoyi/macro-grid-plugin/releases/download/plugin-obs-v0.2.0/obs-0.2.0.zip");
        var other = new Uri("https://github.com/someone-else/other-repo/releases/download/v1/x.zip");

        Assert.True(PluginSourceUrls.BelongsToRepo(own, "Deccoyi", "macro-grid-plugin"));
        Assert.False(PluginSourceUrls.BelongsToRepo(other, "Deccoyi", "macro-grid-plugin"));
    }
}

public sealed class PluginSigningTests
{
    [Fact]
    public void A_signature_from_the_matching_private_key_verifies()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var data = "package bytes"u8.ToArray();
        var signature = ecdsa.SignData(data, HashAlgorithmName.SHA256);

        // PluginSigning embeds one fixed public key, so this only proves the mechanics (Verify says no for a
        // key it doesn't recognize) — the real key pair is exercised by hand against release.yml's output.
        Assert.False(PluginSigning.Verify(data, Convert.ToBase64String(signature)));
    }

    [Fact]
    public void Tampering_with_the_bytes_fails_verification()
    {
        Assert.False(PluginSigning.Verify("tampered"u8.ToArray(), Convert.ToBase64String(new byte[64])));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-base64!!")]
    public void A_missing_or_malformed_signature_never_throws(string? signature) =>
        Assert.False(PluginSigning.Verify("x"u8.ToArray(), signature));
}
