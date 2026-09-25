namespace MacroGrid.Core.Updates;

/// <summary>
/// How the app starts the installer for an update. The setup is <b>not</b> silent: it shows a progress window, and it shows the user agreement
/// when the text changed since the person accepted it (a silent setup would skip that page). <c>/UPDATE</c> is the switch the setup script reads
/// (<c>IsUpdateRun</c>) to skip its other pages and to start the app again afterwards.
/// </summary>
public static class InstallerArguments
{
    public const string ForUpdate = "/UPDATE /NORESTART";
}
