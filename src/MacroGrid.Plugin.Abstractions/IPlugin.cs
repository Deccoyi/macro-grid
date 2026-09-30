namespace MacroGrid.Plugin.Abstractions;

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

    /// <summary>The plugin's own install folder (e.g. `%AppData%/MacroGrid/plugins/&lt;id&gt;/`) — writable,
    /// no admin rights needed. A plugin that needs to persist its own settings (connection details, etc.)
    /// reads/writes its own file(s) here; the host has no generic settings UI/storage for plugins yet.</summary>
    string DataDirectory { get; }

    /// <summary>Writes a line to the host's plugin log, prefixed with the plugin's id. There is no
    /// per-plugin log level — use sparingly (connection state changes, errors), not for high-frequency polling.</summary>
    void Log(string message);

    void RegisterAction(IActionHandler handler);

    /// <summary>Registers a variable provider. If it also implements <see cref="IVariableCatalogSource"/>, that is picked up automatically.</summary>
    void RegisterVariableProvider(IVariableProvider provider);

    /// <summary>Registers the plugin's own settings window, drawn by the host from the page's <c>Fields</c>.</summary>
    void RegisterSettingsPage(IPluginSettingsPage page);

    /// <summary>Creates one entry the plugin owns in the editor's window-wide status bar. Call once per
    /// logical status ("demo-connection"); reuse the returned item across the plugin's lifetime.</summary>
    IPluginStatusItem CreateStatusItem(string id);

    /// <summary>Registers a set of icons the plugin contributes to the editor's icon picker.</summary>
    void RegisterIconPack(IIconPackSource iconPack);

    /// <summary>Protects secret values (for example a password field) the plugin writes to its own settings
    /// file, so that copying the data folder does not copy a usable secret. Optional: a plugin that stores its
    /// settings as plain JSON without using this keeps working exactly as before.</summary>
    IPluginSecrets Secrets { get; }

    /// <summary>Pushes events to the plugin's own custom widgets (<see cref="PluginManifest.Widgets"/>).</summary>
    IPluginWidgets Widgets { get; }

    /// <summary>Reports problems the person can fix to the editor's Error List. Has a default (nothing is reported), so a test double that does
    /// not care keeps compiling; the real host always overrides it. Error lines are display only and never stop anything.</summary>
    IPluginDiagnostics Diagnostics => NullPluginDiagnostics.Instance;
}

/// <summary>Encrypts one plugin's own secret before it is written to disk and decrypts it when read back. Backed
/// by the same protection the host uses for its own secrets (DPAPI for the current Windows user on Windows) —
/// not plugin-specific encryption, and not a secure enclave: it only keeps a secret from being usable if the data
/// folder is copied elsewhere or read by another account.</summary>
public interface IPluginSecrets
{
    string Protect(string secret);

    /// <summary>Null when the value cannot be decrypted here (a different Windows user, a different PC, or data this host did not protect).</summary>
    string? Unprotect(string protectedSecret);
}
