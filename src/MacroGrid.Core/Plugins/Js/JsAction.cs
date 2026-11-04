using System.Text.Json.Nodes;
using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Core.Plugins.Js;

/// <summary>What a script passes to <c>host.registerAction</c> (everything except the run function).</summary>
internal sealed record JsActionMeta(string Type, string? Name, string? Category, string? Description, string? Icon, SettingField[]? Fields, bool Outcome = false);

internal sealed class JsAction(JsPlugin plugin, JsActionMeta meta) : IActionHandler, IActionDescriptor, IActionOutcomeHandler, IOptionsSearch
{
    public string Type => meta.Type;
    public string DisplayName => string.IsNullOrWhiteSpace(meta.Name) ? meta.Type : meta.Name;
    public string Category => string.IsNullOrWhiteSpace(meta.Category) ? "Plugins" : meta.Category;
    public string? Description => meta.Description;
    public string? Icon => meta.Icon;
    public IReadOnlyList<SettingField> Fields => meta.Fields ?? [];

    /// <summary>The older, unsearched call: the first page.</summary>
    public Task<OptionsResult> GetOptionsAsync(string sourceId, JsonObject currentValues, CancellationToken cancellationToken) =>
        SearchOptionsAsync(sourceId, currentValues, new OptionsQuery("", 0, OptionsQuery.MaxLimit, null), cancellationToken);

    public Task<OptionsResult> SearchOptionsAsync(string sourceId, JsonObject currentValues, OptionsQuery query, CancellationToken cancellationToken) =>
        plugin.SearchOptionsAsync(sourceId, currentValues, query, cancellationToken);

    public async Task ExecuteAsync(ActionContext context, JsonObject settings, CancellationToken cancellationToken) =>
        (await ExecuteWithOutcomeAsync(context, settings, cancellationToken)).ThrowIfFailed();

    public Task<ActionOutcome> ExecuteWithOutcomeAsync(ActionContext context, JsonObject settings, CancellationToken cancellationToken) =>
        plugin.RunActionAsync(Type, meta.Outcome, context, settings, cancellationToken);
}
