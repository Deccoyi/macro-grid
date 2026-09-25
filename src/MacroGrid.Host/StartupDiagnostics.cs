using System.Security.Principal;
using MacroGrid.Core.Devices;
using MacroGrid.Core.Diagnostics;
using MacroGrid.Core.Plugins;
using MacroGrid.Core.Preferences;
using MacroGrid.Core.Profiles;
using MacroGrid.Core.Sessions;

namespace MacroGrid.Host;

/// <summary>
/// Writes what this start sees to the log (category "Startup"): who runs it, from where, which data folder it uses, what that folder
/// holds and what of it was loaded. Nothing here can stop the app from starting; every step is wrapped.
/// </summary>
internal static class StartupDiagnostics
{
    /// <summary>Right after the host is built, before anything is started: if the app hangs while starting, this line is the last one in the log.</summary>
    public static void LogStarting(IServiceProvider services, string[] args)
    {
        Run(services, logger =>
        {
            var dataDir = ProfileStore.DefaultDataDir;
            logger.LogInformation("Macro Grid {Version} is starting. Exe: {Exe}. Arguments: {Args}", ClientHub.ServerVersion, Environment.ProcessPath, args.Length == 0 ? "(none)" : string.Join(' ', args));
            logger.LogInformation("Running as {User} (administrator: {Admin}), OS {Os}, {Bitness}-bit process, session {Session}", $"{Environment.UserDomainName}\\{Environment.UserName}", IsAdministrator(), Environment.OSVersion.VersionString, Environment.Is64BitProcess ? 64 : 32, Environment.GetEnvironmentVariable("SESSIONNAME") ?? "?");
            logger.LogInformation("Data folder: {Dir} (Roaming AppData folder: {Roaming}, exists: {Exists})", dataDir, Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Directory.Exists(dataDir));
            foreach (var line in DataDirReport.Inventory(dataDir)) Write(logger, line);
        });
    }

    /// <summary>After the host has started (stores are read, plugins are loaded): what was found on disk against what is in memory.</summary>
    public static void LogStarted(IServiceProvider services)
    {
        Run(services, logger =>
        {
            var dataDir = ProfileStore.DefaultDataDir;
            var profiles = services.GetRequiredService<ProfileStore>();
            foreach (var line in DataDirReport.CompareProfiles(dataDir, [.. profiles.All.Select(p => p.Id)])) Write(logger, line);

            var plugins = services.GetRequiredService<PluginManager>();
            foreach (var line in DataDirReport.ComparePlugins(dataDir, plugins.Plugins.ToDictionary(p => p.Id, p => p.Status.ToString(), StringComparer.OrdinalIgnoreCase)))
                Write(logger, line);

            var preferences = services.GetRequiredService<PreferencesStore>().Get();
            logger.LogInformation("Preferences: language {Language}, theme {Theme}, preferences.json exists: {Exists}", preferences.Language, preferences.Theme, File.Exists(Path.Combine(dataDir, "preferences.json")));
            logger.LogInformation("Paired devices: {Count}, devices.json exists: {Exists}", services.GetRequiredService<DeviceStore>().All.Count, File.Exists(Path.Combine(dataDir, "devices.json")));

            var probe = DataDirReport.WriteProbe(dataDir);
            if (probe is null) logger.LogInformation("Write test in the data folder: ok");
            else logger.LogWarning("Write test in the data folder FAILED: {Reason}", probe);
        });
    }

    private static void Run(IServiceProvider services, Action<ILogger> body)
    {
        try
        {
            body(services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup"));
        }
        catch (Exception ex)
        {
            try { services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup").LogWarning(ex, "The startup report failed"); }
            catch (Exception) { /* never stop the app for a diagnostic */ }
        }
    }

    private static void Write(ILogger logger, ReportLine line)
    {
        if (line.Problem) logger.LogWarning("{Line}", line.Text);
        else logger.LogInformation("{Line}", line.Text);
    }

    private static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }
}
