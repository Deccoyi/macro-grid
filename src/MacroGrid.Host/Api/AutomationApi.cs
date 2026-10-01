using MacroGrid.Core.Automation;
using MacroGrid.Core.Variables;
using Microsoft.AspNetCore.Routing;

namespace MacroGrid.Host.Api;

/// <summary>The editor's automation page: the rules and the pause, what each rule did last, "Run now", and which rules use a variable. Loopback-only, like the rest of <c>/api</c>.</summary>
internal static class AutomationApi
{
    public static RouteGroupBuilder MapAutomationApi(this RouteGroupBuilder api)
    {
        api.MapGet("/automation", (AutomationStore store, AutomationService service) => ApiResults.Json(new
        {
            paused = store.Paused,
            rules = store.List(),
            limits = new
            {
                maxRules = AutomationLimits.MaxRules,
                maxNameLength = AutomationLimits.MaxNameLength,
                maxSteps = AutomationLimits.MaxSteps,
                maxCooldownSeconds = AutomationLimits.MaxCooldownSeconds,
                maxComparisons = AutomationLimits.MaxComparisons,
            },
            status = service.Status(),
        }));

        // The whole list at once, like the variable list: all of it is valid or none of it is taken.
        api.MapPut("/automation", async (HttpRequest request, AutomationStore store) =>
        {
            if (request.ContentLength > AutomationLimits.MaxFileBytes) return ApiResults.BadRequest("The rules are too large.");
            var (valid, body) = await ApiResults.ReadJsonAsync<AutomationBody>(request);
            if (!valid || body?.Rules is null) return ApiResults.InvalidJson();
            return store.TryReplace(body.Rules, body.Paused, out var error) ? Results.NoContent() : ApiResults.BadRequest(error);
        });

        api.MapPost("/automation/{id}/run", (string id, AutomationService service) =>
            service.RunNow(id, out var message) switch
            {
                AutomationRunResult.Started => Results.NoContent(),
                AutomationRunResult.NotFound => Results.NotFound(),
                _ => Results.Conflict(new { error = message }),
            });

        // Which rules mention a variable, so deleting it is warned about (the profiles are searched by /user-variables/usage).
        api.MapGet("/automation/usage", (string name, AutomationStore store) =>
            ApiResults.Json(UserVariableUsage.FindInRules(store.List(), UserVariables.Prefix + name)));

        return api;
    }

    private sealed record AutomationBody(List<AutomationRule>? Rules, bool Paused);
}
