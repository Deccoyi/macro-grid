namespace MacroGrid.Core.Security;

/// <summary>Encrypts a secret before it is written to disk, so that copying the data folder does not copy a usable
/// secret. On Windows this is DPAPI for the current Windows user (<c>MacroGrid.Windows.Security.DpapiSecretProtector</c>).</summary>
public interface ISecretProtector
{
    string Protect(string secret);

    /// <summary>Null when the value cannot be decrypted here (another Windows user, another PC, damaged data).</summary>
    string? Unprotect(string protectedSecret);

    /// <summary>Like <see cref="Protect(string)"/>, but bound to <paramref name="scope"/> (for a plugin: its id), so the
    /// value does not decrypt under another scope. The default ignores the scope; a protector that can bind it overrides this.</summary>
    string Protect(string secret, string scope) => Protect(secret);

    /// <summary>Null when the value cannot be decrypted here or was protected under another scope.</summary>
    string? Unprotect(string protectedSecret, string scope) => Unprotect(protectedSecret);
}
