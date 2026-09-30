namespace MacroGrid.Plugin.Abstractions;

/// <summary>How serious a problem a plugin reports is. Shown as the severity of a line in the editor's Error List.</summary>
public enum PluginDiagnosticLevel
{
    Info,
    Warning,
    Error,
}

/// <summary>
/// Lets a plugin put a line in the editor's Error List, for something the person can fix (a wrong password, an address that cannot be reached).
/// Host-implemented. The host shows the line with the plugin's name, removes every line of a plugin when it is reloaded or removed, and keeps
/// the list short: the same message is one line with a count, and a plugin can keep at most 20 different lines at once (more are dropped).
/// </summary>
public interface IPluginDiagnostics
{
    /// <summary>Adds a line. <paramref name="message"/> is cut to 300 characters and control characters are removed. A line with a
    /// <paramref name="key"/> stays until <see cref="Resolve"/> is called with the same key, so a problem that is gone can be taken back.</summary>
    void Report(PluginDiagnosticLevel level, string message, string? key = null);

    /// <summary>Removes the lines that were reported with <paramref name="key"/>.</summary>
    void Resolve(string key);
}
