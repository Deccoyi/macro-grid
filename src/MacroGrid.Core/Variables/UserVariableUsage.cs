using System.Text.Json;
using System.Text.RegularExpressions;
using MacroGrid.Core.Model;
using MacroGrid.Protocol;

namespace MacroGrid.Core.Variables;

/// <summary>Where a widget uses a variable: its text, a dynamic rule, an action's settings or its own properties.</summary>
public enum UserVariableSpot { Text, Dynamic, Action, Props }

public sealed record UserVariableUse(string ProfileId, string ProfileName, string PageId, string PageName, string WidgetId, string WidgetName, UserVariableSpot Spot);

/// <summary>Finds the widgets that mention a variable, so the editor can warn before the person deletes it.</summary>
public static class UserVariableUsage
{
    public const int MaxResults = 50;

    public static IReadOnlyList<UserVariableUse> Find(IEnumerable<Profile> profiles, string fullName)
    {
        // The name may not be followed or preceded by more name characters: user.count is not a use of user.counter.
        var pattern = new Regex($@"(?<![A-Za-z0-9_.]){Regex.Escape(fullName)}(?![A-Za-z0-9_])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var found = new List<UserVariableUse>();

        foreach (var profile in profiles)
            foreach (var page in profile.Pages)
                foreach (var widget in page.Widgets)
                {
                    foreach (var spot in Spots(widget, pattern))
                    {
                        found.Add(new UserVariableUse(profile.Id, profile.Name, page.Id, page.Name, widget.Id, widget.Name ?? widget.Id, spot));
                        if (found.Count >= MaxResults) return found;
                    }
                }
        return found;
    }

    private static IEnumerable<UserVariableSpot> Spots(Widget widget, Regex pattern)
    {
        if (widget.Text is { } text && pattern.IsMatch(text)) yield return UserVariableSpot.Text;
        if (widget.Dynamic.Count > 0 && pattern.IsMatch(JsonSerializer.Serialize(widget.Dynamic, ProtocolJson.Options))) yield return UserVariableSpot.Dynamic;
        if (widget.Actions.Count > 0 && pattern.IsMatch(JsonSerializer.Serialize(widget.Actions, ProtocolJson.Options))) yield return UserVariableSpot.Action;
        if (widget.Props is { } props && pattern.IsMatch(props.ToJsonString())) yield return UserVariableSpot.Props;
    }
}
