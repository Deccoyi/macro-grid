using System.Security.Cryptography;
using System.Text;
using MacroGrid.Core.Security;

namespace MacroGrid.Windows.Security;

/// <summary>Windows DPAPI for the current user: only the same Windows account on the same PC can decrypt, so a copy
/// of the data folder (a backup, another user, another PC) holds no usable token. The value on disk is base64.
/// The unscoped entropy only keeps these values apart from other DPAPI uses; it is public, so it is no protection against
/// other programs running as the same Windows user, which DPAPI cannot give. The scoped overloads (one scope per plugin)
/// derive the entropy from the scope, so a value of one plugin does not decrypt under another plugin's scope.</summary>
public sealed class DpapiSecretProtector : ISecretProtector
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("MacroGrid.DeviceToken.v1");

    /// <summary>Fixed app constant mixed into every scope's entropy.</summary>
    private const string ScopeSalt = "macrogrid.plugin-secret.v2:";

    public string Protect(string secret) => Protect(secret, Entropy);

    public string? Unprotect(string protectedSecret) => Unprotect(protectedSecret, Entropy);

    public string Protect(string secret, string scope) => Protect(secret, ScopeEntropy(scope));

    public string? Unprotect(string protectedSecret, string scope) => Unprotect(protectedSecret, ScopeEntropy(scope));

    private static byte[] ScopeEntropy(string scope) => SHA256.HashData(Encoding.UTF8.GetBytes(ScopeSalt + scope));

    private static string Protect(string secret, byte[] entropy) =>
        Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(secret), entropy, DataProtectionScope.CurrentUser));

    private static string? Unprotect(string protectedSecret, byte[] entropy)
    {
        try
        {
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(protectedSecret), entropy, DataProtectionScope.CurrentUser));
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            return null;
        }
    }
}
