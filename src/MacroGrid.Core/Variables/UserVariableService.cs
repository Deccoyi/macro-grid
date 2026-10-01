using System.Text.Json;
using MacroGrid.Core.Diagnostics;
using MacroGrid.Plugin.Abstractions;
using MacroGrid.Protocol;
using Microsoft.Extensions.Hosting;

namespace MacroGrid.Core.Variables;

/// <summary>The result of changing one user variable's value.</summary>
public enum UserVariableWrite { Ok, NotFound, WrongType, InvalidValue }

/// <summary>
/// Keeps the variables the person defines (<c>user.&lt;name&gt;</c>), publishes them into the shared <see cref="VariableStore"/> and
/// saves the definitions, and the current values of the variables marked "keep", in the data folder.
/// </summary>
public sealed class UserVariableService : IVariableCatalogSource, IHostedService, IDisposable
{
    private static readonly JsonSerializerOptions FileJson = new(ProtocolJson.Options) { WriteIndented = true };
    private static readonly TimeSpan SaveDelay = TimeSpan.FromSeconds(2);

    private sealed record DefinitionsFile(int FormatVersion, List<UserVariable> Variables);
    private sealed record ValuesFile(int FormatVersion, Dictionary<string, JsonElement> Values);

    private readonly VariableStore _store;
    private readonly TimeProvider _clock;
    private readonly string _definitionsPath;
    private readonly string _valuesPath;
    private readonly Lock _lock = new();
    private readonly Dictionary<string, UserVariable> _definitions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, object?> _values = new(StringComparer.OrdinalIgnoreCase);
    private ITimer? _saveTimer;

    public UserVariableService(string dataDir, VariableStore store, TimeProvider? clock = null)
    {
        Directory.CreateDirectory(dataDir);
        _store = store;
        _clock = clock ?? TimeProvider.System;
        _definitionsPath = Path.Combine(dataDir, "user-variables.json");
        _valuesPath = Path.Combine(dataDir, "user-variable-values.json");
        Load();
    }

    /// <summary>The definitions, in the order the person arranged them.</summary>
    public IReadOnlyList<UserVariable> List()
    {
        lock (_lock) return [.. _order.Select(n => _definitions[n])];
    }

    private List<string> _order = [];

    public UserVariable? Find(string fullName)
    {
        lock (_lock) return TryDefinition(fullName, out var def) ? def : null;
    }

    /// <summary>Replaces the whole list. Nothing changes when any part is invalid. A variable's name and type are fixed: a different type under an existing name is refused.</summary>
    public bool TryReplace(IReadOnlyList<UserVariable> list, out string error)
    {
        if (list.Count > UserVariables.MaxCount) { error = $"At most {UserVariables.MaxCount} variables."; return false; }
        var normalized = new List<UserVariable>(list.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        lock (_lock)
        {
            foreach (var v in list)
            {
                if (!UserVariables.IsValidName(v.Name)) { error = $"'{v.Name}' is not a valid name. Use a letter, then letters, digits or '_' (at most {UserVariables.MaxNameLength})."; return false; }
                if (!seen.Add(v.Name)) { error = $"'{v.Name}' is listed twice."; return false; }
                if (!UserVariables.IsSupportedType(v.Type)) { error = $"'{v.Name}' has an unsupported type."; return false; }
                if (_definitions.TryGetValue(v.Name, out var existing) && existing.Type != v.Type) { error = $"The type of '{v.Name}' cannot change."; return false; }
                if (!TryInitial(v, out var initial)) { error = $"'{v.Name}' has a start value that does not fit its type."; return false; }
                normalized.Add(new UserVariable(existing?.Name ?? v.Name, v.Type, initial, v.Keep, PlainText.Clean(v.Description, UserVariables.MaxDescriptionLength)));
            }

            var removed = _definitions.Keys.Where(n => !seen.Contains(n)).ToList();
            _definitions.Clear();
            foreach (var v in normalized) _definitions[v.Name] = v;
            _order = [.. normalized.Select(v => v.Name)];
            foreach (var name in removed)
            {
                _values.Remove(name);
                _store.Remove(UserVariables.Prefix + name);
            }
            foreach (var v in normalized)
                if (!_values.ContainsKey(v.Name)) Publish(v, v.Initial);
            WriteFile(_definitionsPath, new DefinitionsFile(1, normalized));
            ScheduleValueSave();
        }
        error = "";
        return true;
    }

    public UserVariableWrite Set(string fullName, object? raw)
    {
        lock (_lock)
        {
            if (!TryDefinition(fullName, out var def)) return UserVariableWrite.NotFound;
            if (!UserVariables.TryConvert(def.Type, raw, out var value)) return UserVariableWrite.InvalidValue;
            Publish(def, value);
            ScheduleValueSave();
            return UserVariableWrite.Ok;
        }
    }

    public UserVariableWrite Toggle(string fullName)
    {
        lock (_lock)
        {
            if (!TryDefinition(fullName, out var def)) return UserVariableWrite.NotFound;
            if (def.Type != VariableType.Boolean) return UserVariableWrite.WrongType;
            Publish(def, _values.GetValueOrDefault(def.Name) is not true);
            ScheduleValueSave();
            return UserVariableWrite.Ok;
        }
    }

    /// <summary>Adds <paramref name="amount"/> (may be negative) to a Number; a variable without a value counts as 0.</summary>
    public UserVariableWrite Add(string fullName, double amount)
    {
        lock (_lock)
        {
            if (!TryDefinition(fullName, out var def)) return UserVariableWrite.NotFound;
            if (def.Type != VariableType.Number) return UserVariableWrite.WrongType;
            var result = (_values.GetValueOrDefault(def.Name) is double d ? d : 0) + amount;
            if (!double.IsFinite(result)) return UserVariableWrite.InvalidValue;
            Publish(def, result);
            ScheduleValueSave();
            return UserVariableWrite.Ok;
        }
    }

    public UserVariableWrite Reset(string fullName)
    {
        lock (_lock)
        {
            if (!TryDefinition(fullName, out var def)) return UserVariableWrite.NotFound;
            Publish(def, def.Initial);
            ScheduleValueSave();
            return UserVariableWrite.Ok;
        }
    }

    public IEnumerable<VariableInfo> Describe()
    {
        lock (_lock)
            return [.. _order.Select(n => _definitions[n]).Select(v => new VariableInfo(
                UserVariables.Prefix + v.Name, v.Description, "{" + UserVariables.Prefix + v.Name + "}", UserVariables.Category) { Type = v.Type })];
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken)
    {
        FlushValues();
        return Task.CompletedTask;
    }

    public void Dispose() => _saveTimer?.Dispose();

    /// <summary>Writes the kept values now instead of waiting for the short delay after the last change.</summary>
    public void FlushValues()
    {
        lock (_lock)
        {
            _saveTimer?.Dispose();
            _saveTimer = null;
            var values = new Dictionary<string, JsonElement>();
            foreach (var def in _definitions.Values)
                if (def.Keep && _values.GetValueOrDefault(def.Name) is { } value)
                    values[def.Name] = JsonSerializer.SerializeToElement(value, FileJson);
            WriteFile(_valuesPath, new ValuesFile(1, values));
        }
    }

    private static bool TryInitial(UserVariable v, out object? initial)
    {
        initial = null;
        var none = v.Initial is null or JsonElement { ValueKind: JsonValueKind.Null }
            || (v.Initial is string { Length: 0 } && v.Type != VariableType.Text);
        if (none)
        {
            if (v.Type == VariableType.Text) initial = "";
            return true;
        }
        return UserVariables.TryConvert(v.Type, v.Initial, out initial);
    }

    private bool TryDefinition(string fullName, out UserVariable definition)
    {
        definition = null!;
        return UserVariables.IsUserName(fullName) && _definitions.TryGetValue(fullName[UserVariables.Prefix.Length..], out definition!);
    }

    private void Publish(UserVariable def, object? value)
    {
        _values[def.Name] = value;
        _store.Set(UserVariables.Prefix + def.Name, value);
    }

    /// <summary>The delayed save: a disk problem must not crash the server; the values are written again on the next change or when the server stops.</summary>
    private void TryFlushValues()
    {
        try { FlushValues(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    private void ScheduleValueSave()
    {
        _saveTimer?.Dispose();
        _saveTimer = _clock.CreateTimer(_ => TryFlushValues(), null, SaveDelay, Timeout.InfiniteTimeSpan);
    }

    private void Load()
    {
        var definitions = ReadFile<DefinitionsFile>(_definitionsPath);
        if (definitions?.Variables is null) return;
        var values = ReadFile<ValuesFile>(_valuesPath)?.Values;
        foreach (var v in definitions.Variables.Take(UserVariables.MaxCount))
        {
            if (!UserVariables.IsValidName(v.Name) || !UserVariables.IsSupportedType(v.Type) || _definitions.ContainsKey(v.Name)) continue;
            if (!TryInitial(v, out var initial)) initial = v.Type == VariableType.Text ? "" : null;
            var def = new UserVariable(v.Name, v.Type, initial, v.Keep, PlainText.Clean(v.Description, UserVariables.MaxDescriptionLength));
            _definitions[def.Name] = def;
            _order.Add(def.Name);
            var current = initial;
            if (def.Keep && values is not null && values.TryGetValue(def.Name, out var saved) && UserVariables.TryConvert(def.Type, saved, out var restored))
                current = restored;
            Publish(def, current);
        }
    }

    private static T? ReadFile<T>(string path) where T : class
    {
        if (!File.Exists(path)) return null;
        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), FileJson);
        }
        catch (JsonException)
        {
            File.Move(path, path + ".broken", overwrite: true);
            return null;
        }
    }

    private static void WriteFile<T>(string path, T content)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(content, FileJson));
        File.Move(tmp, path, overwrite: true);
    }
}
