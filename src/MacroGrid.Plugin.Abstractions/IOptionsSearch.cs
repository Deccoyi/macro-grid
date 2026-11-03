using System.Text.Json.Nodes;

namespace MacroGrid.Plugin.Abstractions;

/// <summary>What the editor asks of a searchable options source. The constructor clamps every value, so a source never sees
/// a negative offset, a limit outside 1-50, or text longer than <see cref="MaxTextLength"/>.</summary>
public sealed record OptionsQuery
{
    public const int MaxTextLength = 100;
    public const int MaxLimit = 50;
    public const int MaxSelectedLength = 512;

    public OptionsQuery(string? text, int offset, int limit, string? selected)
    {
        var trimmed = (text ?? "").Trim();
        Text = trimmed.Length > MaxTextLength ? trimmed[..MaxTextLength].TrimEnd() : trimmed;
        Offset = Math.Max(0, offset);
        Limit = Math.Clamp(limit, 1, MaxLimit);
        Selected = string.IsNullOrEmpty(selected) || selected.Length > MaxSelectedLength ? null : selected;
    }

    /// <summary>True when the person pressed the refresh button: re-read the data behind the list instead of answering from a cache. A source that
    /// has to do this asynchronously answers at once with its current options and <see cref="OptionsResult.Refreshing"/> set. An init property,
    /// so a plugin built against an earlier SDK keeps working.</summary>
    public bool Refresh { get; init; }

    /// <summary>What the person typed, trimmed; empty when nothing was typed.</summary>
    public string Text { get; }

    /// <summary>How many options the caller already has (the selected one included), so the next page starts here.</summary>
    public int Offset { get; }

    /// <summary>The most options to return (1-50).</summary>
    public int Limit { get; }

    /// <summary>The value stored in the field right now. On the first page (offset 0) put this option first, counted in <see cref="Limit"/> and in
    /// <see cref="Offset"/> of the next page, so the editor can show its label, and set <see cref="OptionsResult.SelectedMissing"/> when it is gone. Null when nothing is stored.</summary>
    public string? Selected { get; }
}

/// <summary>An <see cref="IOptionsSource"/> that can search and page for a field marked <see cref="SettingField.Searchable"/>. A source that
/// does not implement it still works: the host calls <see cref="IOptionsSource.GetOptionsAsync"/> and filters and pages the whole list itself.</summary>
public interface IOptionsSearch : IOptionsSource
{
    Task<OptionsResult> SearchOptionsAsync(string sourceId, JsonObject currentValues, OptionsQuery query, CancellationToken cancellationToken);
}
