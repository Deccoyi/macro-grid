using System.Reflection;
using System.Runtime.Loader;
using MacroStation.Plugin.Abstractions;

namespace MacroStation.Core.Plugins;

/// <summary>
/// One isolated <see cref="AssemblyLoadContext"/> per plugin (C# plugins get isolation, not a sandbox — full CLR access, but a crashing/leaking plugin doesn't take down the
/// default context or collide with another plugin's own dependency versions). Collectible so a plugin can be
/// unloaded and reloaded without restarting the server. Managed assemblies are loaded from memory, not from
/// the file, so the plugin's DLLs are never locked on disk: a plugin can be replaced or deleted while (or
/// right after) it ran. The trade-off is that <c>Assembly.Location</c> is empty inside a plugin — use
/// <see cref="MacroStation.Plugin.Abstractions.IPluginHost.DataDirectory"/> to find its files instead.
/// </summary>
internal sealed class PluginLoadContext(string pluginId, string entryDllPath) : AssemblyLoadContext(name: $"plugin:{pluginId}", isCollectible: true)
{
    /// <summary>
    /// Must stay a single shared instance across the host and every plugin — otherwise a plugin's
    /// `is IPlugin`/`is IActionHandler` checks fail against the host's own copies of these interfaces
    /// (same type name, different load context = different CLR type identity).
    /// </summary>
    private static readonly string SharedAssemblyName = typeof(IPlugin).Assembly.GetName().Name!;

    private readonly AssemblyDependencyResolver _resolver = new(entryDllPath);

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (assemblyName.Name == SharedAssemblyName)
            return null; // fall through to the Default context, which already has it loaded

        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        return path is null ? null : LoadPluginAssembly(path);
    }

    internal Assembly LoadPluginAssembly(string path)
    {
        using var stream = new MemoryStream(File.ReadAllBytes(path));
        return LoadFromStream(stream);
    }

    protected override nint LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path is null ? nint.Zero : LoadUnmanagedDllFromPath(path);
    }
}
