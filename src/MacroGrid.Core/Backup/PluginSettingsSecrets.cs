using System.Text.Json.Nodes;
using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Core.Backup;

/// <summary>Removes the values of password fields from a plugin's settings, at the top level and inside the rows of a list field, so a backup never holds one.</summary>
public static class PluginSettingsSecrets
{
    public static JsonObject Strip(IReadOnlyList<SettingField> fields, JsonObject values)
    {
        var copy = (JsonObject)values.DeepClone();
        StripInto(fields, copy);
        return copy;
    }

    /// <summary>Restoring settings from a backup: a password the backup does not hold keeps the value stored here.</summary>
    public static void KeepStoredPasswords(IReadOnlyList<SettingField> fields, JsonObject values, JsonObject stored)
    {
        foreach (var field in fields)
            if (field.Kind == SettingFieldKind.Password && !values.ContainsKey(field.Key) && stored[field.Key] is { } current)
                values[field.Key] = current.DeepClone();
    }

    private static void StripInto(IReadOnlyList<SettingField> fields, JsonObject values)
    {
        foreach (var field in fields)
        {
            if (field.Kind == SettingFieldKind.Password)
            {
                values.Remove(field.Key);
            }
            else if (field.Kind == SettingFieldKind.List && field.ItemFields is { Length: > 0 } itemFields && values[field.Key] is JsonArray rows)
            {
                foreach (var row in rows)
                    if (row is JsonObject rowValues) StripInto(itemFields, rowValues);
            }
        }
    }
}
