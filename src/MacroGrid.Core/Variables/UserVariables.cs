using System.Globalization;
using System.Text.Json;
using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Core.Variables;

/// <summary>A variable the person defines in the Global Variable List. The name and type never change after creation.</summary>
/// <param name="Initial">The start value as the editor sent it (a string, number or boolean); null means "no value yet", which a Number or Boolean shows as unavailable.</param>
/// <param name="Keep">True when the current value is saved and comes back after a restart; otherwise it starts again from <paramref name="Initial"/>.</param>
public sealed record UserVariable(string Name, VariableType Type, object? Initial = null, bool Keep = false, string Description = "");

/// <summary>The rules for names, values and limits of the variables a person defines. Their store names are <c>user.&lt;name&gt;</c>.</summary>
public static class UserVariables
{
    public const string Prefix = "user.";
    public const string Category = "Global Variable List";
    public const int MaxCount = 200;
    public const int MaxNameLength = 40;
    public const int MaxDescriptionLength = 200;
    public const int MaxTextLength = 1024;

    public static bool IsUserName(string? fullName) => fullName is not null && fullName.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);

    public static bool IsValidName(string? name)
    {
        if (string.IsNullOrEmpty(name) || name.Length > MaxNameLength || !char.IsAsciiLetter(name[0])) return false;
        foreach (var c in name)
            if (!(char.IsAsciiLetterOrDigit(c) || c == '_')) return false;
        return true;
    }

    public static bool IsSupportedType(VariableType type) => type is VariableType.Text or VariableType.Number or VariableType.Boolean;

    /// <summary>Converts a value from a person, a template or a file to the stored form of <paramref name="type"/>: string, double or bool.</summary>
    public static bool TryConvert(VariableType type, object? raw, out object? value)
    {
        value = null;
        if (raw is JsonElement element)
        {
            raw = element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null,
            };
        }
        if (raw is null) return false;

        switch (type)
        {
            case VariableType.Text:
                var text = raw switch
                {
                    string s => s,
                    bool b => b ? "true" : "false",
                    IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
                    _ => null,
                };
                if (text is null || text.Length > MaxTextLength) return false;
                value = text;
                return true;
            case VariableType.Number:
                double number;
                switch (raw)
                {
                    case double d: number = d; break;
                    case float or int or long or short or byte or decimal:
                        number = Convert.ToDouble(raw, CultureInfo.InvariantCulture); break;
                    case string s when double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed): number = parsed; break;
                    default: return false;
                }
                if (!double.IsFinite(number)) return false;
                value = number;
                return true;
            case VariableType.Boolean:
                switch (raw)
                {
                    case bool b: value = b; return true;
                    case string s:
                        switch (s.Trim().ToLowerInvariant())
                        {
                            case "true" or "1" or "on" or "yes": value = true; return true;
                            case "false" or "0" or "off" or "no": value = false; return true;
                        }
                        return false;
                    default: return false;
                }
            default:
                return false;
        }
    }
}
