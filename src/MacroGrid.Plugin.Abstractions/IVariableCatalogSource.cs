using System.Text.Json.Serialization;

namespace MacroGrid.Plugin.Abstractions;

/// <summary>What a variable's live value is, so the editor can show it and offer the right value input in a condition.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<VariableType>))]
public enum VariableType
{
    [JsonStringEnumMemberName("text")] Text,
    [JsonStringEnumMemberName("number")] Number,
    [JsonStringEnumMemberName("boolean")] Boolean,
    [JsonStringEnumMemberName("duration")] Duration,
    [JsonStringEnumMemberName("dateTime")] DateTime,
}

/// <summary>
/// One variable a provider makes available, described for the editor's variable picker.
/// <paramref name="Category"/> groups it in that picker (e.g. "System"); a plugin can introduce its own
/// category name freely — the editor just lists whatever distinct category strings show up.
/// </summary>
/// <remarks>
/// <see cref="Type"/>, <see cref="Unit"/> and <see cref="Values"/> are init properties rather than constructor
/// parameters, so plugins built against an older SDK (which call the four-argument constructor) keep loading.
/// </remarks>
public sealed record VariableInfo(string Name, string Description, string Example, string Category)
{
    /// <summary>What the live value is. Defaults to <see cref="VariableType.Text"/>.</summary>
    public VariableType Type { get; init; } = VariableType.Text;

    /// <summary>Unit of a number, shown next to the value input (e.g. "%", "GB", "kbps").</summary>
    public string? Unit { get; init; }

    /// <summary>The allowed values of a fixed-choice text variable (e.g. a status); the editor offers them as a list.</summary>
    public IReadOnlyList<string>? Values { get; init; }
}

/// <summary>
/// Optional companion to <see cref="IVariableProvider"/>: lets the editor list "what variables can I
/// use in a widget's text" without having to guess from currently-live values (which may be empty
/// or zero at edit time). A provider implements both interfaces from the same instance.
/// </summary>
public interface IVariableCatalogSource
{
    IEnumerable<VariableInfo> Describe();
}
