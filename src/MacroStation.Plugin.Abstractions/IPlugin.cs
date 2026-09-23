namespace MacroStation.Plugin.Abstractions;

/// <summary>
/// Entry point for a C# plugin (see plugin.json's <c>kind: "csharp"</c>). The host instantiates exactly
/// one implementation per plugin assembly (found by reflection) and calls <see cref="Initialize"/> once,
/// before the plugin's registrations are added to the host's DI container.
/// </summary>
public interface IPlugin
{
    void Initialize(IPluginHost host);
}

/// <summary>
/// What a plugin's <see cref="IPlugin.Initialize"/> gets to register itself with. Registrations are
/// collected, not applied immediately — the host adds them to its own service container after every
/// plugin's Initialize has run, the same way built-in actions/providers are registered.
/// </summary>
public interface IPluginHost
{
    /// <summary>The running server's own version (semver) — matches versioning.md's "Server (Host)" row.</summary>
    string ServerVersion { get; }

    /// <summary>The Plugin SDK (this assembly's) version the plugin was validated against.</summary>
    string SdkVersion { get; }

    /// <summary>The plugin's own install folder (e.g. `%AppData%/MacroStation/plugins/&lt;id&gt;/`) — writable,
    /// no admin rights needed. A plugin that needs to persist its own settings (connection details, etc.)
    /// reads/writes its own file(s) here; the host has no generic settings UI/storage for plugins yet.</summary>
    string DataDirectory { get; }

    /// <summary>Writes a line to the host's plugin log, prefixed with the plugin's id. There is no
    /// per-plugin log level — use sparingly (connection state changes, errors), not for high-frequency polling.</summary>
    void Log(string message);

    void RegisterAction(IActionHandler handler);

    /// <summary>Registers a variable provider. If it also implements <see cref="IVariableCatalogSource"/>, that is picked up automatically.</summary>
    void RegisterVariableProvider(IVariableProvider provider);
}
