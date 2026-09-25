using System.Security.Cryptography;

namespace MacroGrid.Core.Plugins.Distribution;

/// <summary>
/// Verifies the official plugin source's signature: the plugin-signing repository secret's matching public key,
/// embedded here since it is not a secret. See the plugin repository's website/reference/source-index.md for how
/// <c>release.yml</c> produces the signature (ECDSA P-256 over the release zip's own bytes, SHA-256, base64).
/// A signature on a non-official source's entry is never checked here or trusted as official — see
/// <see cref="PluginCatalogClient"/>, which only asks for verification when the source is the built-in official one.
/// </summary>
public static class PluginSigning
{
    /// <summary>SubjectPublicKeyInfo (DER), base64. Public by design; only the matching private key (a GitHub Actions
    /// secret in the plugins repository) can produce a signature this verifies.</summary>
    private const string PublicKeyBase64 =
        "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEeRPznqxspydIc9Iq5a56OywKWDzxY9ILuCeWBBAcfFJHM2LWIeKXvRxwVyTPHA/zayTob/+TMxn7DWVGD5tyXw==";

    /// <summary>True when <paramref name="signatureBase64"/> is a valid ECDSA P-256/SHA-256 signature (IEEE P1363,
    /// 64 bytes) over <paramref name="packageBytes"/> made by the official plugin-signing key. Any malformed input
    /// (bad base64, wrong length, ...) is treated as a failed verification, never an exception.</summary>
    public static bool Verify(byte[] packageBytes, string? signatureBase64)
    {
        if (string.IsNullOrWhiteSpace(signatureBase64)) return false;
        try
        {
            var signature = Convert.FromBase64String(signatureBase64);
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(PublicKeyBase64), out _);
            return ecdsa.VerifyData(packageBytes, signature, HashAlgorithmName.SHA256);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            return false;
        }
    }
}
