using System.Reflection;
using System.Text.Json;
using MacroStation.Plugin.Abstractions;
using Microsoft.Extensions.Logging;

namespace MacroStation.Core.Plugins;

public sealed record PluginLoadResult(IReadOnlyList<LoadedPlugin> Plugins, IReadOnlyList<IActionHandler> Actions, IReadOnlyList<IVariableProvider> VariableProviders);

/// <summary>
/// Scans a `plugins/&lt;Name&gt;/` folder tree for `plugin.json` manifests and loads each `kind: "csharp"`
/// plugin into its own <see cref="PluginLoadContext"/> (agent-and-repo-rules.md madde 2/4). Runs once at
/// startup, before the host builds its DI container — see ServerApp.cs. `kind: "js"` plugins are
/// recognized (so they show up in the list) but not executed yet; that's the Jint sandbox, Aşama 7.
/// </summary>
public static class PluginLoader
{
    private static readonly JsonSerializerOptions ManifestJson = new(JsonSerializerDefaults.Web);

    public static PluginLoadResult LoadAll(string pluginsRoot, string serverVersion, ILogger logger)
    {
        var plugins = new List<LoadedPlugin>();
        var actions = new List<IActionHandler>();
        var providers = new List<IVariableProvider>();
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(pluginsRoot))
            return new PluginLoadResult(plugins, actions, providers);

        foreach (var dir in Directory.EnumerateDirectories(pluginsRoot))
        {
            var manifestPath = Path.Combine(dir, "plugin.json");
            if (!File.Exists(manifestPath))
                continue;

            var folderName = Path.GetFileName(dir);
            PluginManifest manifest;
            try
            {
                manifest = JsonSerializer.Deserialize<PluginManifest>(File.ReadAllText(manifestPath), ManifestJson)
                    ?? throw new JsonException("plugin.json boş");
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException)
            {
                logger.LogError(ex, "Plugin manifest'i okunamadı: {Folder}", folderName);
                plugins.Add(new LoadedPlugin(folderName, folderName, "?", PluginLoadStatus.Error, $"plugin.json ayrıştırılamadı: {ex.Message}"));
                continue;
            }

            if (!seenIds.Add(manifest.Id))
            {
                logger.LogError("Plugin id çakışması: {Id} ({Folder})", manifest.Id, folderName);
                plugins.Add(new LoadedPlugin(manifest.Id, manifest.Name, manifest.Version, PluginLoadStatus.Error, "Bu id zaten yüklü bir plugin tarafından kullanılıyor"));
                continue;
            }

            if (!SemVer.SatisfiesCaret(PluginSdk.Version, manifest.SdkVersion))
            {
                plugins.Add(new LoadedPlugin(manifest.Id, manifest.Name, manifest.Version, PluginLoadStatus.Incompatible,
                    $"SDK {manifest.SdkVersion} istiyor, sunucudaki SDK {PluginSdk.Version}"));
                continue;
            }

            if (!SemVer.SatisfiesMinimum(serverVersion, manifest.MinServerVersion))
            {
                plugins.Add(new LoadedPlugin(manifest.Id, manifest.Name, manifest.Version, PluginLoadStatus.Incompatible,
                    $"Sunucu {manifest.MinServerVersion}+ istiyor, mevcut sunucu {serverVersion}"));
                continue;
            }

            if (manifest.Kind == PluginKind.Js)
            {
                // Recognized but not executable yet — JS/Jint runtime is Aşama 7.
                plugins.Add(new LoadedPlugin(manifest.Id, manifest.Name, manifest.Version, PluginLoadStatus.Incompatible,
                    "JS plugin çalıştırma motoru henüz yok (Aşama 7)"));
                continue;
            }

            var entryPath = Path.Combine(dir, manifest.Entry);
            if (!File.Exists(entryPath))
            {
                plugins.Add(new LoadedPlugin(manifest.Id, manifest.Name, manifest.Version, PluginLoadStatus.Error, $"Giriş dosyası bulunamadı: {manifest.Entry}"));
                continue;
            }

            try
            {
                var context = new PluginLoadContext(manifest.Id, entryPath);
                var assembly = context.LoadFromAssemblyPath(entryPath);
                var pluginType = assembly.GetTypes().FirstOrDefault(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
                    ?? throw new InvalidOperationException($"{manifest.Entry} içinde IPlugin uygulayan bir tip bulunamadı");

                var instance = (IPlugin)(Activator.CreateInstance(pluginType) ?? throw new InvalidOperationException("Plugin örneği oluşturulamadı"));
                var host = new PluginHostCollector(serverVersion);
                instance.Initialize(host);

                actions.AddRange(host.Actions);
                providers.AddRange(host.VariableProviders);
                plugins.Add(new LoadedPlugin(manifest.Id, manifest.Name, manifest.Version, PluginLoadStatus.Loaded, null));
                logger.LogInformation("Plugin yüklendi: {Id} {Version}", manifest.Id, manifest.Version);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Plugin yüklenemedi: {Id}", manifest.Id);
                plugins.Add(new LoadedPlugin(manifest.Id, manifest.Name, manifest.Version, PluginLoadStatus.Error, ex.Message));
            }
        }

        return new PluginLoadResult(plugins, actions, providers);
    }
}
