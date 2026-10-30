using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Plugin.Testing;

/// <summary>In-memory <see cref="IVariableStore"/>. Records every Set and Remove, so a test can assert on what a plugin published
/// (also a brief state), not only on the current values.</summary>
public sealed class FakeVariableStore : IVariableStore
{
    private readonly ConcurrentDictionary<string, object?> _values = new();
    private readonly ConcurrentQueue<(string Name, object? Value)> _sets = new();
    private readonly ConcurrentQueue<string> _removed = new();

    public IReadOnlyCollection<string> RemovedNames => [.. _removed];

    public void Set(string name, object? value)
    {
        _values[name] = value;
        _sets.Enqueue((name, value));
    }

    /// <summary>True if <paramref name="name"/> was ever set to <paramref name="value"/>, even if it changed again since.</summary>
    public bool WasEverSet(string name, object? value) => _sets.Any(s => s.Name == name && Equals(s.Value, value));

    public object? Get(string name) => _values.TryGetValue(name, out var v) ? v : null;

    public void Remove(string name)
    {
        _values.TryRemove(name, out _);
        _removed.Enqueue(name);
    }
}

/// <summary>An <see cref="IPluginHost"/> that keeps everything a plugin registers in plain lists. A new member of the interface that has no
/// default body fails the build of this project, which is the point of keeping it next to the SDK.</summary>
public sealed class FakePluginHost(string dataDirectory) : IPluginHost
{
    public string ServerVersion => "0.0.0-test";
    public string SdkVersion => "0.0.0-test";
    public string DataDirectory { get; } = dataDirectory;
    public IPluginSecrets Secrets { get; } = new FakePluginSecrets();
    public IPluginWidgets Widgets { get; } = new FakePluginWidgets();

    public List<string> Logs { get; } = [];
    public List<IActionHandler> Actions { get; } = [];
    public List<IVariableProvider> VariableProviders { get; } = [];
    public List<IIconPackSource> IconPacks { get; } = [];
    public Dictionary<string, FakeStatusItem> StatusItems { get; } = [];
    public IPluginSettingsPage? SettingsPage { get; private set; }

    public FakePluginWidgets WidgetPosts => (FakePluginWidgets)Widgets;

    public void Log(string message) => Logs.Add(message);
    public void RegisterAction(IActionHandler handler) => Actions.Add(handler);
    public void RegisterVariableProvider(IVariableProvider provider) => VariableProviders.Add(provider);
    public void RegisterSettingsPage(IPluginSettingsPage page) => SettingsPage = page;
    public void RegisterIconPack(IIconPackSource iconPack) => IconPacks.Add(iconPack);

    public IPluginStatusItem CreateStatusItem(string id)
    {
        var item = new FakeStatusItem();
        StatusItems[id] = item;
        return item;
    }
}

/// <summary>Reversible and obviously not the plain secret, so a test can see the round trip went through this. Not real protection.</summary>
public sealed class FakePluginSecrets : IPluginSecrets
{
    private const string Prefix = "test-protected:";

    public string Protect(string secret) => Prefix + secret;

    public string? Unprotect(string protectedSecret) =>
        protectedSecret.StartsWith(Prefix, StringComparison.Ordinal) ? protectedSecret[Prefix.Length..] : null;
}

/// <summary>Remembers the last text and level a plugin showed.</summary>
public sealed class FakeStatusItem : IPluginStatusItem
{
    public string? LastText { get; private set; }
    public StatusLevel? LastLevel { get; private set; }

    public void Update(string text, StatusLevel level, string? icon = null, string? tooltip = null)
    {
        LastText = text;
        LastLevel = level;
    }
}

/// <summary>Records each message a plugin posts to its widgets.</summary>
public sealed class FakePluginWidgets : IPluginWidgets
{
    public sealed record Post(string Widget, string Name, JsonNode? Data, string? WidgetId, string? DeviceId, bool Retain);

    public List<Post> Posts { get; } = [];

    void IPluginWidgets.Post(string widget, string name, JsonNode? data, string? widgetId, string? deviceId, bool retain) =>
        Posts.Add(new Post(widget, name, data, widgetId, deviceId, retain));
}
