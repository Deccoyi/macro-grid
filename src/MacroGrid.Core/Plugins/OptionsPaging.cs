using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MacroGrid.Plugin.Abstractions;
using MacroGrid.Protocol;

namespace MacroGrid.Core.Plugins;

/// <summary>Runs one options request against a source and enforces what the host promises the editor: search and paging for a source
/// that cannot do it itself, and a ceiling on the size of every answer. It never logs: option labels can be people's names or file
/// names, so they stay out of the log (the errors it returns carry no label either).</summary>
public static class OptionsPaging
{
    /// <summary>The most options one answer may hold (not counting the selected one a first page puts in front).</summary>
    public const int MaxOptions = 50;

    /// <summary>The largest answer, as serialized JSON.</summary>
    public const int MaxBytes = 256 * 1024;

    /// <param name="query">Null for a caller that sends no search parameters (an older editor or plugin): the source's list is returned
    /// whole, only the size ceiling applies.</param>
    public static async Task<OptionsResult> RunAsync(IOptionsSource source, string sourceId, JsonObject currentValues, OptionsQuery? query, CancellationToken ct)
    {
        if (query is null)
            return Ceiling(await source.GetOptionsAsync(sourceId, currentValues, ct));

        if (source is IOptionsSearch search)
        {
            var answer = await search.SearchOptionsAsync(sourceId, currentValues, query, ct);
            if (answer.Options.Count > MaxOptions + 1)
                return Failed($"The options source returned more than {MaxOptions} options for one page.");
            return Ceiling(answer);
        }

        var all = await source.GetOptionsAsync(sourceId, currentValues, ct);
        if (all.Error is not null) return all;
        return Ceiling(Page(all.Options, query));
    }

    /// <summary>Case- and diacritic-insensitive "contains" on the label, then one page. The stored value goes first on page 0 and is left
    /// out of later pages; <c>offset</c> counts the matches without it.</summary>
    internal static OptionsResult Page(IReadOnlyList<SettingOption> all, OptionsQuery query)
    {
        var needle = Fold(query.Text);
        var selected = query.Selected is null ? null : all.FirstOrDefault(o => o.Value == query.Selected);
        var matches = all.Where(o => !ReferenceEquals(o, selected) && (needle.Length == 0 || Fold(o.Label).Contains(needle, StringComparison.Ordinal))).ToList();

        var page = matches.Skip(query.Offset).Take(query.Limit).ToList();
        if (query.Offset == 0 && selected is not null) page.Insert(0, selected);
        return new OptionsResult(page)
        {
            HasMore = matches.Count > query.Offset + query.Limit,
            SelectedMissing = query.Offset == 0 && query.Selected is not null ? selected is null : null,
        };
    }

    /// <summary>Lower case, without accents, with the Turkish dotted and dotless i treated as i.</summary>
    internal static string Fold(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var c in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            builder.Append(c is 'ı' or 'İ' ? 'i' : char.ToLowerInvariant(c));
        }
        return builder.ToString();
    }

    private static OptionsResult Ceiling(OptionsResult answer) =>
        Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(answer, ProtocolJson.Options)) > MaxBytes
            ? Failed($"The options answer is larger than {MaxBytes / 1024} KB.")
            : answer;

    private static OptionsResult Failed(string message) => new([], message);
}
