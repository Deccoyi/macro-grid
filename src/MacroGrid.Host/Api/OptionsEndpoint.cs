using MacroGrid.Core.Plugins;
using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Host.Api;

/// <summary>Shared handler for the "dynamic options" endpoints of actions and plugin settings pages.</summary>
internal static class OptionsEndpoint
{
    /// <param name="source">The options source, or null when the target does not offer dynamic options.</param>
    /// <param name="unsupportedMessage">Error text returned (with an empty option list) when <paramref name="source"/> is null.</param>
    /// <param name="ownerPluginId">Resolves the plugin whose locale translates a returned error message.</param>
    public static async Task<IResult> HandleAsync(
        IOptionsSource? source, string unsupportedMessage, string sourceId, HttpRequest request,
        PluginLocalizer localizer, Func<string?> ownerPluginId)
    {
        if (source is null)
            return Results.Json(new OptionsResult([], unsupportedMessage));

        var (valid, currentValues) = await ApiResults.ReadJsonAsync<System.Text.Json.Nodes.JsonObject>(request);
        if (!valid) return ApiResults.InvalidJson();

        try
        {
            var result = await source.GetOptionsAsync(sourceId, currentValues ?? [], request.HttpContext.RequestAborted);
            return ApiResults.Json(result with { Error = localizer.Translate(ownerPluginId(), result.Error) });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Results.Json(new OptionsResult([], ex.Message));
        }
    }
}
