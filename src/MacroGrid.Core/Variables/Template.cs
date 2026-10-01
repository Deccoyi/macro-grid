using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Core.Variables;

/// <summary>
/// A widget's <c>Text</c>, pre-parsed once and cached by its source string. Supports
/// <c>{name}</c>, <c>{name|format}</c> and <c>{name|format|placeholder}</c> tokens (the placeholder is shown, as plain text, while the variable is unavailable) plus <c>{{</c>/<c>}}</c> for a literal brace.
/// An unmatched or empty <c>{}</c> token is left as-is instead of throwing, since it is user-typed text.
/// </summary>
public sealed class Template
{
    private static readonly ConcurrentDictionary<string, Template> Cache = new();

    private readonly object[] _parts; // string (literal) or VariableRef
    public IReadOnlyList<string> VariableNames { get; }

    /// <summary>The editor saves every intermediate text of a widget, so the cache is cleared when it grows past this
    /// (a cleared entry is only parsed again).</summary>
    private const int MaxCachedTemplates = 4096;

    public static Template Parse(string text)
    {
        if (Cache.TryGetValue(text, out var cached)) return cached;
        if (Cache.Count >= MaxCachedTemplates) Cache.Clear();
        return Cache.GetOrAdd(text, static t => new Template(ParseParts(t)));
    }

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
            else if (part is VariableRef v)
            {
                var value = store.Get(v.Name);
                if (value is null && v.Placeholder is not null) sb.Append(v.Placeholder);
                else sb.Append(FormatValue(value, v.Format));
            }
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
                string? format = null, placeholder = null;
                if (pipe >= 0)
                {
                    var rest = inner[(pipe + 1)..];
                    var second = rest.IndexOf('|');
                    format = second < 0 ? rest : rest[..second];
                    if (second >= 0) placeholder = Truncate(rest[(second + 1)..]);
                }
                parts.Add(new VariableRef(name, format, placeholder));
                i = end + 1;
                continue;
            }

            literal.Append(c);
            i++;
        }

        if (literal.Length > 0) parts.Add(literal.ToString());
        return parts.ToArray();
    }

    /// <summary>A placeholder is literal text: it is never parsed for tokens again and is cut here so a long one cannot fill a widget.</summary>
    private const int MaxPlaceholderLength = 64;

    private static string Truncate(string placeholder) =>
        placeholder.Length <= MaxPlaceholderLength ? placeholder : placeholder[..MaxPlaceholderLength];

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
        return value ? AppLanguage.Pick("On", "Açık") : AppLanguage.Pick("Off", "Kapalı");
    }

    private sealed record VariableRef(string Name, string? Format, string? Placeholder);
}
