using System.Reflection;
using MacroGrid.Core.Plugins;
using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Tests;

public sealed class PluginLoadContextTests
{
    [Theory]
    [InlineData("0.0.1.0")]
    [InlineData("0.3.0.0")]
    [InlineData("9.9.9.9")]
    public void The_shared_sdk_resolves_to_the_hosts_copy_whatever_version_the_plugin_asked_for(string requestedVersion)
    {
        var context = new PluginLoadContext("test", typeof(PluginLoadContextTests).Assembly.Location);
        var name = new AssemblyName("MacroGrid.Plugin.Abstractions") { Version = Version.Parse(requestedVersion) };

        var resolved = context.LoadFromAssemblyName(name);

        Assert.Same(typeof(IPlugin).Assembly, resolved);
        context.Unload();
    }
}
