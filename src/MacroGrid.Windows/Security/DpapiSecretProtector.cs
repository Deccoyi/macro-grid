using System.Security.Cryptography;
using System.Text;
using MacroGrid.Core.Security;

namespace MacroGrid.Windows.Security;

/// <summary>Windows DPAPI for the current user: only the same Windows account on the same PC can decrypt, so a copy
/// of the data folder (a backup, another user, another PC) holds no usable token. The value on disk is base64.
/// The entropy only keeps these values apart from other DPAPI uses; it is public, so it is no protection against
/// other programs running as the same Windows user, which DPAPI cannot give.</summary>
public sealed class DpapiSecretProtector : ISecretProtector
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("MacroGrid.DeviceToken.v1");

    public string Protect(string secret) =>
        Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(secret), Entropy, DataProtectionScope.CurrentUser));

    public string? Unprotect(string protectedSecret)
    {
        try
        {
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(protectedSecret), Entropy, DataProtectionScope.CurrentUser));
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            return null;
        }
    }
}
