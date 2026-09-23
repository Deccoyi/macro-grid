using MacroStation.Core.Obs;

namespace MacroStation.Tests;

public class ObsAuthTests
{
    [Fact]
    public void ComputeAuthenticationString_matches_obs_websocket_formula()
    {
        // Reference vector computed independently in Python (hashlib/base64) following the exact steps from
        // https://github.com/obsproject/obs-websocket/blob/master/docs/generated/protocol.md#creating-an-authentication-string
        // — not a value obs-websocket itself published, just a cross-check that this implementation follows
        // "base64(sha256(password + salt))" then "base64(sha256(secret + challenge))" in that order.
        const string password = "mypassword";
        const string salt = "4/nUvOwJ8x9RTQVX4G3TEzZ1CVEAcv7dyzKQeUt2Q2U=";
        const string challenge = "ymDD6RtWNqz4d3jXKu9KOAAjXFsQZgqz6DBpKzHz0IU=";
        const string expected = "H0pLPU1tlxTZHLRMVLj8U/Jj4MyK4+E+vLyLabifMdw=";

        Assert.Equal(expected, ObsAuth.ComputeAuthenticationString(password, salt, challenge));
    }

    [Fact]
    public void ComputeAuthenticationString_is_sensitive_to_password()
    {
        var a = ObsAuth.ComputeAuthenticationString("password1", "salt", "challenge");
        var b = ObsAuth.ComputeAuthenticationString("password2", "salt", "challenge");

        Assert.NotEqual(a, b);
    }
}
