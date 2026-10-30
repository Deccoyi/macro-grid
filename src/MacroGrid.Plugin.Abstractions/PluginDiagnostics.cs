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
    /// <summary>Adds a line. <paramref name="message"/> is cut to 300 characters; control, direction-changing and zero-width characters are removed,
    /// and it is shown as plain text. With a <paramref name="key"/> (up to 64 characters of letters, digits, '.', '_' and '-') the call is an upsert: the
    /// line of that key is replaced, not added again, and it stays until <see cref="Resolve"/> or <see cref="Clear"/>. Without a key, the same level
    /// and message is one line with a count, which only <see cref="Clear"/> removes. Bad input is dropped without an exception; a plugin may make
    /// about 10 calls a second, more are dropped.</summary>
    void Report(PluginDiagnosticLevel level, string message, string? key = null);

    /// <summary>Removes the lines that were reported with <paramref name="key"/>.</summary>
    void Resolve(string key);

    /// <summary>Removes every line this plugin reported.</summary>
    void Clear();
}

/// <summary>A <see cref="IPluginDiagnostics"/> that does nothing; what a host that has no Error List (a test double) hands out.</summary>
public sealed class NullPluginDiagnostics : IPluginDiagnostics
{
    public static readonly NullPluginDiagnostics Instance = new();

    private NullPluginDiagnostics() { }

    public void Report(PluginDiagnosticLevel level, string message, string? key = null) { }
    public void Resolve(string key) { }
    public void Clear() { }
}
