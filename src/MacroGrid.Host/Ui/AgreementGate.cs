using MacroGrid.Core.Preferences;
using MacroGrid.Windows.Agreement;

namespace MacroGrid.Host.Ui;

/// <summary>
/// Makes sure the person using this copy has accepted the user agreement it ships, before the server starts: no listener, no actions, no devices,
/// no update check and no notification exist until they have. The setup records the acceptance for the person who installed or updated; another
/// Windows user of the same PC (or anyone after the text changed) is asked here, once. A build without the agreement file (a development build) asks nothing.
/// </summary>
internal static class AgreementGate
{
    public const string FileName = "license-agreement.txt";

    /// <summary>True when the app may go on. False means the person declined (or closed the window) and the app must exit.</summary>
    public static bool EnsureAccepted(string baseDirectory, AgreementRecord record)
    {
        var path = Path.Combine(baseDirectory, FileName);
        if (!File.Exists(path)) return true;

        var bytes = File.ReadAllBytes(path);
        var shipped = AgreementAcceptance.HashOf(bytes);
        if (AgreementAcceptance.IsAccepted(record.ReadHash(), shipped)) return true;

        using var dialog = new AgreementDialog(System.Text.Encoding.UTF8.GetString(bytes));
        if (dialog.ShowDialog() != DialogResult.OK) return false;

        record.Write(shipped, DateTimeOffset.Now);
        return true;
    }
}
