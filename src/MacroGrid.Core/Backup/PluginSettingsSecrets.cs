using System.Text.Json.Nodes;
using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Core.Backup;

/// <summary>The one walker over the password fields of a plugin's settings, at the top level and inside the rows of a list field.
/// A backup never holds a password, the editor never gets one back, and a form saved with blank passwords keeps the stored ones.</summary>
public static class PluginSettingsSecrets
{
    /// <summary>Calls <paramref name="visit"/> with the object that holds each password field (the settings or a list row) and the field, present or not.</summary>
    public static void ForEachPassword(IReadOnlyList<SettingField> fields, JsonObject values, Action<JsonObject, SettingField> visit)
    {
        foreach (var field in fields)
        {
            if (field.Kind == SettingFieldKind.Password)
            {
                visit(values, field);
            }
            else if (field.Kind == SettingFieldKind.List && field.ItemFields is { Length: > 0 } itemFields && values[field.Key] is JsonArray rows)
            {
                foreach (var row in rows)
                    if (row is JsonObject rowValues) ForEachPassword(itemFields, rowValues, visit);
            }
        }
    }

    public static JsonObject Strip(IReadOnlyList<SettingField> fields, JsonObject values)
    {
        var copy = (JsonObject)values.DeepClone();
        ForEachPassword(fields, copy, (holder, field) => holder.Remove(field.Key));
        return copy;
    }

    /// <summary>Blanks every password that is present (in place), so the editor never receives an existing one.</summary>
    public static JsonObject Blank(IReadOnlyList<SettingField> fields, JsonObject values)
    {
        ForEachPassword(fields, values, (holder, field) =>
        {
            if (holder.ContainsKey(field.Key)) holder[field.Key] = "";
        });
        return values;
    }

    /// <summary>True when a password is missing or empty somewhere in <paramref name="values"/>.</summary>
    public static bool HasBlankPassword(IReadOnlyList<SettingField> fields, JsonObject values)
    {
        var blank = false;
        ForEachPassword(fields, values, (holder, field) => blank |= IsBlank(holder[field.Key]));
        return blank;
    }

    /// <summary>A password that is missing or empty in <paramref name="values"/> (a form the editor sent, or a backup) takes the stored one.
    /// A list row only takes it from the stored row at the same position, and only when everything else in the two rows is equal, so a
    /// secret never moves to another row (a reordered or deleted row ends up empty).</summary>
    public static void RestoreBlankPasswords(IReadOnlyList<SettingField> fields, JsonObject values, JsonObject stored)
    {
        foreach (var field in fields)
        {
            if (field.Kind == SettingFieldKind.Password)
            {
                if (IsBlank(values[field.Key]) && stored[field.Key] is { } current && !IsBlank(current))
                    values[field.Key] = current.DeepClone();
            }
            else if (field.Kind == SettingFieldKind.List && field.ItemFields is { Length: > 0 } itemFields
                && values[field.Key] is JsonArray rows && stored[field.Key] is JsonArray storedRows)
            {
                for (var i = 0; i < rows.Count && i < storedRows.Count; i++)
                    if (rows[i] is JsonObject row && storedRows[i] is JsonObject storedRow
                        && JsonNode.DeepEquals(Strip(itemFields, row), Strip(itemFields, storedRow)))
                        RestoreBlankPasswords(itemFields, row, storedRow);
            }
        }
    }

    private static bool IsBlank(JsonNode? value) =>
        value is null || (value is JsonValue v && v.TryGetValue<string>(out var text) && text.Length == 0);
}
