using Microsoft.Win32;

namespace MacroGrid.Windows.Autostart;

/// <summary>
/// The per-user "start Macro Grid when I sign in to Windows" switch. It is the same <c>Run</c> registry value the installer's
/// task writes, so the wizard choice and the Preferences window always agree, and a value the user removed in Windows'
/// startup settings shows up as off.
/// </summary>
public sealed class AutostartService(
    string executablePath,
    string runKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run",
    string valueName = "MacroGrid")
{
    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(runKeyPath);
        return key?.GetValue(valueName) is string command && !string.IsNullOrWhiteSpace(command);
    }

    public void SetEnabled(bool enabled)
    {
        if (enabled)
        {
            using var key = Registry.CurrentUser.CreateSubKey(runKeyPath);
            // The argument tells the app it was started by Windows, not by the person (see StartupPolicy in Core).
            key.SetValue(valueName, $"\"{executablePath}\" --autostart");
            return;
        }

        using var existing = Registry.CurrentUser.OpenSubKey(runKeyPath, writable: true);
        existing?.DeleteValue(valueName, throwOnMissingValue: false);
    }
}
