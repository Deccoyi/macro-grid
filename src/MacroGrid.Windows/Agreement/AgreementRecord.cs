using Microsoft.Win32;

namespace MacroGrid.Windows.Agreement;

/// <summary>
/// The user agreement this Windows user has accepted, kept in <c>HKCU\Software\Macro Grid</c> (per person, not per PC). The installer writes the
/// same <c>AcceptedAgreement</c> value when the person accepts its license page, so a person who installed or updated is not asked again in the app.
/// </summary>
public sealed class AgreementRecord(string keyPath = @"Software\Macro Grid")
{
    private const string HashValue = "AcceptedAgreement";
    private const string DateValue = "AcceptedAgreementDate";

    /// <summary>The recorded hash, or null when this person has accepted nothing yet.</summary>
    public string? ReadHash()
    {
        using var key = Registry.CurrentUser.OpenSubKey(keyPath);
        return key?.GetValue(HashValue) as string;
    }

    public void Write(string hash, DateTimeOffset when)
    {
        using var key = Registry.CurrentUser.CreateSubKey(keyPath);
        key.SetValue(HashValue, hash);
        key.SetValue(DateValue, when.ToString("O"));
    }
}
