using System.Reflection;
using System.Security.Cryptography;
using System.Runtime.Loader;
using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Core.Plugins;

/// <summary>
/// One isolated <see cref="AssemblyLoadContext"/> per plugin (C# plugins get isolation, not a sandbox — full CLR access, but a crashing/leaking plugin doesn't take down the
/// default context or collide with another plugin's own dependency versions). Collectible so a plugin can be
/// unloaded and reloaded without restarting the server. Managed assemblies are loaded from memory, not from
/// the file, so the plugin's DLLs are never locked on disk: a plugin can be replaced or deleted while (or
/// right after) it ran. The trade-off is that <c>Assembly.Location</c> is empty inside a plugin — use
/// <see cref="MacroGrid.Plugin.Abstractions.IPluginHost.DataDirectory"/> to find its files instead.
/// </summary>
///
/// With <paramref name="signedFiles"/> (an official, signed plugin) only files the signature lists are ever loaded, and each
/// one is hashed again right before it is loaded: a plugin cannot write a DLL into its own folder later and have it resolved,
/// and a file swapped after the check is refused. Without it (an unsigned plugin in a development build) nothing is restricted.
internal sealed class PluginLoadContext(string pluginId, string entryDllPath, IReadOnlyDictionary<string, string>? signedFiles = null, string? pluginDir = null) : AssemblyLoadContext(name: $"plugin:{pluginId}", isCollectible: true)
{
    /// <summary>
    /// Must stay a single shared instance across the host and every plugin — otherwise a plugin's
    /// `is IPlugin`/`is IActionHandler` checks fail against the host's own copies of these interfaces
    /// (same type name, different load context = different CLR type identity).
    /// </summary>
    private static readonly Assembly SharedAssembly = typeof(IPlugin).Assembly;

    private readonly AssemblyDependencyResolver _resolver = new(entryDllPath);

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Hand out the host's own copy whatever version the plugin was built against. Returning null here would leave
        // the version check to the default context, and a single-file publish refuses a plugin built against an older
        // patch version of the SDK (0.3.0.0 requested, 0.3.1.0 present), and the same holds across major
        // versions (0.4.0.0 requested, 1.0.0.0 present). Whether a plugin is compatible is decided by the minMacroGrid version in
        // its manifest (PluginCompatibility), not by the assembly version.
        if (assemblyName.Name == SharedAssembly.GetName().Name)
            return SharedAssembly;

        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        if (path is null || !IsSigned(path)) return null;
        return LoadPluginAssembly(path);
    }

    internal Assembly LoadPluginAssembly(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (!IsSigned(path, bytes))
            throw new InvalidOperationException($"'{Path.GetFileName(path)}' is not part of the plugin's signature.");
        using var stream = new MemoryStream(bytes);
        return LoadFromStream(stream);
    }

    protected override nint LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path is null || !IsSigned(path) ? nint.Zero : LoadUnmanagedDllFromPath(path);
    }

    /// <summary>True when there is no restriction, or the file is listed in the signature and still has the listed hash.</summary>
    private bool IsSigned(string path, byte[]? bytes = null)
    {
        if (signedFiles is null) return true;
        var relative = Path.GetRelativePath(Path.GetFullPath(pluginDir ?? Path.GetDirectoryName(entryDllPath)!), Path.GetFullPath(path)).Replace('\\', '/');
        if (!signedFiles.TryGetValue(relative, out var expected)) return false;
        return string.Equals(Convert.ToHexString(SHA256.HashData(bytes ?? File.ReadAllBytes(path))), expected, StringComparison.OrdinalIgnoreCase);
    }
}
