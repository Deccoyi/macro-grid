using System.Text.Json.Nodes;
using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Tests.StubPlugin;

/// <summary>Loaded by PluginLoaderTests from a compiled plugin.json + DLL pair, to exercise the SDK 0.3.0
/// schema-driven registrations end to end (a real assembly load, not a mock). It also opts in to the optional
/// tree items (<see cref="IPluginTreeProvider"/> with <see cref="IPluginTreeItemSettings"/>): a "sounds" folder
/// with two items that have their own settings; saving an item's name raises <see cref="TreeItemsChanged"/>.</summary>
public sealed class StubPlugin : IPlugin, IPluginTreeProvider, IPluginTreeItemSettings
{
    private readonly Dictionary<string, string> _soundNames = new() { ["s1"] = "Applause", ["s2"] = "Drum roll" };

    public event Action<string?>? TreeItemsChanged;

    public void Initialize(IPluginHost host)
    {
        host.RegisterAction(new StubAction());
        host.RegisterVariableProvider(new StubProvider());
        host.RegisterSettingsPage(new StubSettingsPage());
        host.CreateStatusItem("stub").Update("stub ready", StatusLevel.Ok);
    }

    public Task<PluginTreePage> GetTreeItemsAsync(string? parentId, string? continuationToken, CancellationToken cancellationToken) =>
        Task.FromResult(parentId switch
        {
            null => new PluginTreePage([new PluginTreeItem("sounds", "Sounds", "folder", HasChildren: true)]),
            "sounds" => new PluginTreePage([.. _soundNames.Select(kv => new PluginTreeItem(kv.Key, kv.Value, "music", HasSettings: true))]),
            _ => new PluginTreePage([]),
        });

    public IReadOnlyList<SettingField> GetItemFields(string itemId) =>
        _soundNames.ContainsKey(itemId) ? [new SettingField("name", "Name", SettingFieldKind.Text)] : throw new KeyNotFoundException($"No item '{itemId}'.");

    public JsonObject LoadItem(string itemId) => new() { ["name"] = _soundNames[itemId] };

    public void SaveItem(string itemId, JsonObject values)
    {
        _soundNames[itemId] = values["name"]?.GetValue<string>() ?? _soundNames[itemId];
        TreeItemsChanged?.Invoke("sounds");
    }
}

public sealed class StubAction : IActionHandler, IActionDescriptor, IActionOutcomeHandler
{
    public string Type => "stub.action";
    public string DisplayName => "Stub aksiyon";
    public string Category => "Stub";
    public string? Description => "Action for tests";
    public string? Icon => "flask-conical";
    public IReadOnlyList<SettingField> Fields => [new SettingField("value", "Value", SettingFieldKind.Text)];

    public Task ExecuteAsync(ActionContext context, JsonObject settings, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Reports a failure when the "value" setting is "missing", so a test can see an outcome cross a real assembly load.</summary>
    public Task<ActionOutcome> ExecuteWithOutcomeAsync(ActionContext context, JsonObject settings, CancellationToken cancellationToken) =>
        Task.FromResult(settings["value"]?.GetValue<string>() == "missing" ? ActionOutcome.Failed(ActionFailureCode.NotFound, "No such item.") : ActionOutcome.Success);
}

public sealed class StubSettingsPage : IPluginSettingsPage
{
    public IReadOnlyList<SettingField> Fields => [new SettingField("enabled", "Etkin", SettingFieldKind.Bool)];

    public JsonObject Load() => new() { ["enabled"] = true };

    public void Save(JsonObject values) { }
}

/// <summary>Sets one variable and then idles until cancelled, like a real long-running provider.</summary>
public sealed class StubProvider : IVariableProvider, IVariableCatalogSource
{
    public IEnumerable<VariableInfo> Describe() => [new VariableInfo("stub.value", "Test value", "{stub.value}", "Stub")];

    public async Task RunAsync(IVariableStore store, CancellationToken cancellationToken)
    {
        store.Set("stub.value", 42.0);
        await Task.Delay(Timeout.Infinite, cancellationToken);
    }
}
