using System.Text.Json.Nodes;
using MacroStation.Plugin.Abstractions;

namespace MacroStation.Tests.StubPlugin;

/// <summary>Loaded by PluginLoaderTests from a compiled plugin.json + DLL pair, to exercise the SDK 0.3.0
/// schema-driven registrations end to end (a real assembly load, not a mock).</summary>
public sealed class StubPlugin : IPlugin
{
    public void Initialize(IPluginHost host)
    {
        host.RegisterAction(new StubAction());
        host.RegisterSettingsPage(new StubSettingsPage());
        host.CreateStatusItem("stub").Update("stub hazır", StatusLevel.Ok);
    }
}

public sealed class StubAction : IActionHandler, IActionDescriptor
{
    public string Type => "stub.action";
    public string DisplayName => "Stub aksiyon";
    public string Category => "Stub";
    public string? Description => "Test amaçlı aksiyon";
    public string? Icon => "flask-conical";
    public IReadOnlyList<SettingField> Fields => [new SettingField("value", "Değer", SettingFieldKind.Text)];

    public Task ExecuteAsync(ActionContext context, JsonObject settings, CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class StubSettingsPage : IPluginSettingsPage
{
    public IReadOnlyList<SettingField> Fields => [new SettingField("enabled", "Etkin", SettingFieldKind.Bool)];

    public JsonObject Load() => new() { ["enabled"] = true };

    public void Save(JsonObject values) { }
}
