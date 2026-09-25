using System.Text.Json.Nodes;
using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Core.Plugins.Js;

/// <summary>What a script passes to <c>host.registerAction</c> (everything except the run function).</summary>
internal sealed record JsActionMeta(string Type, string? Name, string? Category, string? Description, string? Icon, SettingField[]? Fields);

internal sealed class JsAction(JsPlugin plugin, JsActionMeta meta) : IActionHandler, IActionDescriptor
{
    public string Type => meta.Type;
    public string DisplayName => string.IsNullOrWhiteSpace(meta.Name) ? meta.Type : meta.Name;
    public string Category => string.IsNullOrWhiteSpace(meta.Category) ? "Plugins" : meta.Category;
    public string? Description => meta.Description;
    public string? Icon => meta.Icon;
    public IReadOnlyList<SettingField> Fields => meta.Fields ?? [];

    public Task ExecuteAsync(ActionContext context, JsonObject settings, CancellationToken cancellationToken) =>
        plugin.RunActionAsync(Type, context, settings, cancellationToken);
}
