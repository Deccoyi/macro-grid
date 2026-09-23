using System.Security.Cryptography;
using System.Text;

namespace MacroStation.Core.Obs;

/// <summary>
/// obs-websocket v5 authentication string: base64(sha256(base64(sha256(password + salt)) + challenge)).
/// Both salt and challenge come from the server's `Hello` message. See
/// https://github.com/obsproject/obs-websocket/blob/master/docs/generated/protocol.md#creating-an-authentication-string.
/// </summary>
public static class ObsAuth
{
    public static string ComputeAuthenticationString(string password, string salt, string challenge)
    {
        var secret = Sha256Base64(password + salt);
        return Sha256Base64(secret + challenge);
    }

    private static string Sha256Base64(string value) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
