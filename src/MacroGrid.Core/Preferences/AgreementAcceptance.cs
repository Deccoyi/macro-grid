using System.Security.Cryptography;

namespace MacroGrid.Core.Preferences;

/// <summary>
/// Which user agreement a person has accepted. The agreement is identified by the SHA-256 of the file exactly as it ships (the installer computes the
/// same hash at build time and records it when its license page was accepted), so any edit of the text, even a typo fix, asks everyone again.
/// This class only compares; where the record lives (the registry, per Windows user) is the caller's business.
/// </summary>
public static class AgreementAcceptance
{
    /// <summary>Lower-case hex SHA-256 of the agreement file's bytes.</summary>
    public static string HashOf(ReadOnlySpan<byte> agreementFile) => Convert.ToHexString(SHA256.HashData(agreementFile)).ToLowerInvariant();

    /// <summary>True when the recorded hash is the shipped one. No record (null or empty) is not accepted.</summary>
    public static bool IsAccepted(string? recordedHash, string shippedHash) =>
        !string.IsNullOrWhiteSpace(recordedHash) && string.Equals(recordedHash.Trim(), shippedHash, StringComparison.OrdinalIgnoreCase);
}
