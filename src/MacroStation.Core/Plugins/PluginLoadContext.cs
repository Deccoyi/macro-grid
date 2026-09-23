using System.Reflection;
using System.Runtime.Loader;
using MacroStation.Plugin.Abstractions;

namespace MacroStation.Core.Plugins;

/// <summary>
/// One isolated <see cref="AssemblyLoadContext"/> per plugin (agent-and-repo-rules.md madde 4: C# plugins
/// get isolation, not a sandbox — full CLR access, but a crashing/leaking plugin doesn't take down the
/// default context or collide with another plugin's own dependency versions). Collectible so a future
/// "reload plugin" feature can unload it.
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
        return path is null ? null : LoadFromAssemblyPath(path);
    }

    protected override nint LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path is null ? nint.Zero : LoadUnmanagedDllFromPath(path);
    }
}
