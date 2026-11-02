using MacroGrid.Core.Plugins;
using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Host.Api;

/// <summary>Shared handler for the "dynamic options" endpoints of actions and plugin settings pages. The body is the current form values;
/// <c>?q=&amp;offset=&amp;limit=&amp;selected=</c> ask for one searched page (any of them present makes it a search). Neither the body nor the
/// answer is logged.</summary>
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
            var result = await OptionsPaging.RunAsync(source, sourceId, currentValues ?? [], ReadQuery(request.Query), request.HttpContext.RequestAborted);
            return ApiResults.Json(result with { Error = localizer.Translate(ownerPluginId(), result.Error) });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Results.Json(new OptionsResult([], ex.Message));
        }
    }

    /// <summary>Null when the caller sent no search parameter (the old call: the whole list).</summary>
    private static OptionsQuery? ReadQuery(IQueryCollection query)
    {
        if (!(query.ContainsKey("q") || query.ContainsKey("offset") || query.ContainsKey("limit") || query.ContainsKey("selected"))) return null;
        _ = int.TryParse(query["offset"], out var offset);
        var limit = int.TryParse(query["limit"], out var parsed) ? parsed : OptionsPaging.MaxOptions;
        return new OptionsQuery(query["q"], offset, limit, query["selected"]);
    }
}
