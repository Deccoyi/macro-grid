using MacroGrid.Host.Ui;
using MacroGrid.Core;
using MacroGrid.Core.Actions;
using MacroGrid.Core.Devices;
using MacroGrid.Core.Plugins;
using MacroGrid.Core.Plugins.Distribution;
using MacroGrid.Core.Preferences;
using MacroGrid.Core.Profiles;
using MacroGrid.Core.Sessions;
using MacroGrid.Core.Updates;
using MacroGrid.Host.Updates;
using MacroGrid.Core.Variables;
using MacroGrid.Plugin.Abstractions;
using MacroGrid.Windows.Audio;
using MacroGrid.Windows.Autostart;
using MacroGrid.Windows.Input;
using MacroGrid.Windows.Variables;
using MacroGrid.Windows.Windows;
using MacroGrid.Core.Widgets;

namespace MacroGrid.Host;

/// <summary>Dependency-injection registrations of the host, grouped by concern. The order inside each group is behavior.</summary>
internal static class ServiceRegistration
{
    /// <summary>Stores and settings that everything else builds on.</summary>
    public static IServiceCollection AddHostStores(this IServiceCollection services, string dataDir)
    {
        services.AddSingleton(new ProfileStore(dataDir));
        var preferencesStore = new PreferencesStore(dataDir);
        AppLanguage.Current = preferencesStore.Get().Language;
        preferencesStore.Changed += () => AppLanguage.Current = preferencesStore.Get().Language;
        services.AddSingleton(preferencesStore);
        services.AddSingleton(new LegalDocuments(AppContext.BaseDirectory));
        services.AddSingleton(new AutostartService(Environment.ProcessPath ?? Application.ExecutablePath));
        return services;
    }

    /// <summary>
    /// The built-in actions. Handlers are resolved in registration order and the first one that matches an
    /// action type wins, so keep the order stable.
    /// </summary>
    public static IServiceCollection AddBuiltInActions(this IServiceCollection services, IUiDialogService dialogs, IUiWindowService windows)
    {
        services.AddSingleton<IInputService, WindowsInputService>();
        services.AddActionHandler<HotkeyAction>();
        services.AddActionHandler<TypeTextAction>();
        services.AddActionHandler<PageAction>();
        services.AddActionHandler<ProfileAction>();
        services.AddActionHandler<OpenAction>();
        services.AddActionHandler<OpenUrlAction>();
        services.AddActionHandler<DelayAction>();
        services.AddSingleton<IAudioService, WindowsAudioService>();
        services.AddActionHandler<SetVolumeAction>();
        services.AddActionHandler<SetMuteAction>();
        services.AddActionHandler<ToggleMuteAction>();
        services.AddSingleton<ActionDispatcher>();
        services.AddSingleton(dialogs);
        services.AddSingleton(windows);
        return services;
    }

    /// <summary>The variable store, the built-in system providers and the status registry.</summary>
    public static IServiceCollection AddVariablesAndStatus(this IServiceCollection services)
    {
        services.AddSingleton<VariableStore>();
        services.AddSingleton<IVariableStore>(sp => sp.GetRequiredService<VariableStore>());
        services.AddVariableProvider<SystemMetricsProvider>();
        services.AddVariableProvider<SystemAudioProvider>();
        var statusRegistry = new PluginStatusRegistry();
        services.AddSingleton(statusRegistry);
        statusRegistry.SetCore("server", $"Macro Grid {ClientHub.ServerVersion}", StatusLevel.Idle, "server");
        return services;
    }

    /// <summary>The plugin manager and the services it depends on.</summary>
    public static IServiceCollection AddPlugins(this IServiceCollection services, string dataDir)
    {
        var pluginsRoot = Path.Combine(dataDir, "plugins");

        services.AddSingleton(sp => new PluginLocalizer(() => sp.GetRequiredService<PreferencesStore>().Get().Language));
        services.AddSingleton<VariableCatalog>();
        services.AddHostedSingleton<VariableProviderHost>();
        // Registered after the provider host: loading a plugin starts its variable providers on that host.
        services.AddSingleton(sp => new PluginManager(pluginsRoot, ClientHub.ServerVersion,
            sp.GetRequiredService<PluginStatusRegistry>(), sp.GetRequiredService<ActionDispatcher>(),
            sp.GetRequiredService<VariableCatalog>(), sp.GetRequiredService<VariableProviderHost>(),
            sp.GetRequiredService<VariableStore>(), new PluginPermissionStore(dataDir),
            sp.GetRequiredService<IInputService>(), sp.GetRequiredService<ILogger<PluginManager>>(),
            sp.GetRequiredService<PluginLocalizer>()));
        services.AddHostedService(sp => sp.GetRequiredService<PluginManager>());
        return services;
    }

    /// <summary>
    /// Installing plugins from GitHub (Discover tab): the official catalog today, added sources and direct links
    /// in later phases of the plugin distribution plan. Like <see cref="AddUpdates"/>, this is an opt-in outbound
    /// connection the server never makes on its own — the HTTP client is only ever invoked from
    /// <c>PluginCatalogApi</c>, when the editor opens Discover or starts an install.
    /// </summary>
    public static IServiceCollection AddPluginDistribution(this IServiceCollection services, string dataDir)
    {
        // Redirects are switched off so the downloader can check every hop against the host allow-list itself,
        // the same reasoning as AddUpdates' InstallerDownloader client.
        var http = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromMinutes(2) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"MacroGrid/{ClientHub.ServerVersion}");
        services.AddSingleton(new PluginCatalogClient(http));
        services.AddSingleton(new PluginPackageDownloader(http));
        services.AddSingleton(new PluginInstallOriginStore(dataDir));
        services.AddSingleton(sp => new PluginCatalogInstaller(
            sp.GetRequiredService<PluginPackageDownloader>(),
            sp.GetRequiredService<PluginManager>(),
            sp.GetRequiredService<PluginInstallOriginStore>(),
            Path.Combine(dataDir, "plugins-staging")));
        return services;
    }

    /// <summary>Paired devices, client sessions and everything that pushes state to them.</summary>
    public static IServiceCollection AddClientSessions(this IServiceCollection services, string dataDir)
    {
        services.AddSingleton(new DeviceStore(dataDir));
        services.AddSingleton<PairingService>();

        services.AddSingleton<SessionRegistry>();
        services.AddSingleton<ToggleStateStore>();
        services.AddSingleton<AssetStore>();
        services.AddSingleton<LayoutSender>();
        services.AddHostedSingleton<WidgetStateService>();
        services.AddSingleton<IActiveWindowSource, ForegroundWindowMonitor>();
        services.AddHostedSingleton<AutoProfileSwitcher>();
        services.AddSingleton<ClientHub>();
        return services;
    }

    /// <summary>
    /// The update checks. The server's first connection of its own goes to the GitHub releases list only (see the Security model in
    /// docs/architecture.md); it is off when the person switched automatic checks off.
    /// </summary>
    public static IServiceCollection AddUpdates(this IServiceCollection services, string dataDir)
    {
        services.AddSingleton(new UpdateStateStore(dataDir));
        services.AddSingleton(new UpdatePolicy(TimeProvider.System));
        services.AddSingleton(sp => new UpdateChecker(
            CreateReleaseFeed(),
            sp.GetRequiredService<UpdateStateStore>(),
            sp.GetRequiredService<UpdatePolicy>(),
            ReleaseVersion.TryParse(ClientHub.ServerVersion, out var current) ? current : default,
            TimeProvider.System));
        services.AddHostedSingleton<UpdateService>();
        // Redirects are switched off so the downloader can check every hop against the GitHub host allow-list itself.
        services.AddSingleton(new InstallerDownloader(new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false })
        {
            Timeout = TimeSpan.FromMinutes(10),
        }));
        services.AddSingleton<UpdateInstaller>();
        return services;
    }

    private static ReleaseFeed CreateReleaseFeed()
    {
        var localFeed = Environment.GetEnvironmentVariable(LocalFeedHandler.EnvironmentVariable);
        var http = string.IsNullOrWhiteSpace(localFeed)
            ? new HttpClient()
            : new HttpClient(new LocalFeedHandler(localFeed));
        http.Timeout = TimeSpan.FromSeconds(30);
        // GitHub requires a User-Agent; it names the app and its version and nothing about the person.
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"MacroGrid/{ClientHub.ServerVersion}");
        return new ReleaseFeed(http, new Uri("https://api.github.com/repos/Deccoyi/macro-grid/releases?per_page=50"));
    }

    private static IServiceCollection AddActionHandler<THandler>(this IServiceCollection services)
        where THandler : class, IActionHandler =>
        services.AddSingleton<IActionHandler, THandler>();

    /// <summary>Registers one type as the variable provider and the variable catalog source, sharing a single instance.</summary>
    private static IServiceCollection AddVariableProvider<TProvider>(this IServiceCollection services)
        where TProvider : class, IVariableProvider, IVariableCatalogSource
    {
        services.AddSingleton<TProvider>();
        services.AddSingleton<IVariableProvider>(sp => sp.GetRequiredService<TProvider>());
        services.AddSingleton<IVariableCatalogSource>(sp => sp.GetRequiredService<TProvider>());
        return services;
    }

    /// <summary>Registers a singleton that is also started and stopped with the host.</summary>
    private static IServiceCollection AddHostedSingleton<TService>(this IServiceCollection services)
        where TService : class, IHostedService
    {
        services.AddSingleton<TService>();
        services.AddHostedService(sp => sp.GetRequiredService<TService>());
        return services;
    }
}
