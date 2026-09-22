using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using MacroStation.Plugin.Abstractions;

namespace MacroStation.Core.Variables;

/// <summary>
/// A widget's <c>Text</c>, pre-parsed once and cached by its source string. Supports
/// <c>{name}</c> and <c>{name|format}</c> tokens plus <c>{{</c>/<c>}}</c> for a literal brace.
/// An unmatched or empty <c>{}</c> token is left as-is instead of throwing, since it is user-typed text.
/// </summary>
public sealed class Template
{
    private static readonly ConcurrentDictionary<string, Template> Cache = new();

    private readonly object[] _parts; // string (literal) or VariableRef
    public IReadOnlyList<string> VariableNames { get; }

    public static Template Parse(string text) => Cache.GetOrAdd(text, static t => new Template(ParseParts(t)));

    private Template(object[] parts)
    {
        _parts = parts;
        VariableNames = parts.OfType<VariableRef>().Select(v => v.Name).Distinct().ToList();
    }

    public string Render(IVariableStore store)
    {
        if (_parts.Length == 0) return "";
        if (_parts is [string only]) return only; // fast path: no variables at all

        var sb = new StringBuilder();
        foreach (var part in _parts)
        {
            if (part is string literal) sb.Append(literal);
            else if (part is VariableRef v) sb.Append(FormatValue(store.Get(v.Name), v.Format));
        }
        return sb.ToString();
    }

    private static object[] ParseParts(string text)
    {
        var parts = new List<object>();
        var literal = new StringBuilder();
        var i = 0;

        while (i < text.Length)
        {
            var c = text[i];
            if (c == '{' && i + 1 < text.Length && text[i + 1] == '{') { literal.Append('{'); i += 2; continue; }
            if (c == '}' && i + 1 < text.Length && text[i + 1] == '}') { literal.Append('}'); i += 2; continue; }

            if (c == '{')
            {
                var end = text.IndexOf('}', i + 1);
                if (end < 0) { literal.Append(text, i, text.Length - i); break; } // unmatched '{' -> literal rest

                var inner = text[(i + 1)..end];
                var pipe = inner.IndexOf('|');
                var name = (pipe < 0 ? inner : inner[..pipe]).Trim();
                if (name.Length == 0) { literal.Append(text, i, end - i + 1); i = end + 1; continue; } // "{}" -> literal

                if (literal.Length > 0) { parts.Add(literal.ToString()); literal.Clear(); }
                parts.Add(new VariableRef(name, pipe < 0 ? null : inner[(pipe + 1)..]));
                i = end + 1;
                continue;
            }

            literal.Append(c);
            i++;
        }

        if (literal.Length > 0) parts.Add(literal.ToString());
        return parts.ToArray();
    }

    private static string FormatValue(object? value, string? format)
    {
        return value switch
        {
            null => "",
            double or float or int or long => Convert.ToDouble(value, CultureInfo.InvariantCulture)
                .ToString(string.IsNullOrEmpty(format) ? "0.##" : format, CultureInfo.InvariantCulture),
            DateTime dt => dt.ToString(string.IsNullOrEmpty(format) ? "HH:mm" : format, CultureInfo.InvariantCulture),
            DateTimeOffset dto => dto.ToString(string.IsNullOrEmpty(format) ? "HH:mm" : format, CultureInfo.InvariantCulture),
            TimeSpan ts => FormatTimeSpan(ts, format),
            bool b => FormatBool(b, format),
            _ => value.ToString() ?? "",
        };
    }

    private static string FormatTimeSpan(TimeSpan ts, string? format)
    {
        if (!string.IsNullOrEmpty(format)) return ts.ToString(format, CultureInfo.InvariantCulture);
        var pattern = ts.Days != 0 ? @"d\.hh\:mm\:ss" : @"hh\:mm\:ss";
        return ts.ToString(pattern, CultureInfo.InvariantCulture);
    }

    private static string FormatBool(bool value, string? format)
    {
        if (!string.IsNullOrEmpty(format) && format.Contains('/'))
        {
            var words = format.Split('/', 2);
            return value ? words[0] : words[1];
        }
        return value ? "Açık" : "Kapalı";
    }

    private sealed record VariableRef(string Name, string? Format);
}
