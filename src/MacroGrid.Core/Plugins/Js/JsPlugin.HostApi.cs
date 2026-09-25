using System.Text.Json;
using System.Text.RegularExpressions;
using MacroGrid.Core.Input;
using MacroGrid.Plugin.Abstractions;
using MacroGrid.Protocol;

namespace MacroGrid.Core.Plugins.Js;

public sealed partial class JsPlugin
{
    // ---- host API (each call checks its permission) ----

    private void Require(string permission)
    {
        if (!_permissions.Has(permission))
            throw new JsHostException($"This plugin has not been granted the '{permission}' permission.");
    }

    private void VarSet(string name, object? value)
    {
        Require(JsPermissions.Variables);
        _variables.Set(OwnName(name), value switch
        {
            null => null,
            double or bool or string => value,
            _ => value.ToString(),
        });
    }

    private object? VarGet(string name)
    {
        Require(JsPermissions.Variables);
        return _variables.Get(name);
    }

    private void VarRemove(string name)
    {
        Require(JsPermissions.Variables);
        _variables.Remove(OwnName(name));
    }

    private void VarDescribe(string json)
    {
        Require(JsPermissions.Variables);
        foreach (var info in JsonSerializer.Deserialize<VariableInfo[]>(json, ProtocolJson.Options) ?? [])
        {
            OwnName(info.Name);
            _described.Add(info);
        }
    }

    /// <summary>A plugin may only publish variables under its own id, so it can never overwrite <c>system.cpu</c> or another plugin's values.</summary>
    private string OwnName(string name)
    {
        if (!name.StartsWith(_manifest.Id + ".", StringComparison.Ordinal) || name.Length > 120 || !VariableName().IsMatch(name))
            throw new JsHostException($"Variable names must start with '{_manifest.Id}.' and use only letters, digits, '.', '_' and '-'.");
        return name;
    }

    private void RegisterAction(string json)
    {
        Require(JsPermissions.Actions);
        var meta = JsonSerializer.Deserialize<JsActionMeta>(json, ProtocolJson.Options)
            ?? throw new JsHostException("registerAction needs a definition.");
        if (string.IsNullOrWhiteSpace(meta.Type) || !meta.Type.StartsWith(_manifest.Id + ".", StringComparison.Ordinal))
            throw new JsHostException($"Action types must start with '{_manifest.Id}.'.");
        _host!.RegisterAction(new JsAction(this, meta));
    }

    private void SettingsPage(string json)
    {
        var fields = JsonSerializer.Deserialize<SettingField[]>(json, ProtocolJson.Options)
            ?? throw new JsHostException("settings.page needs a list of fields.");
        _settingsPage = new JsSettingsPage(_host!.DataDirectory, fields);
    }

    private void Status(string id, string text, string level)
    {
        if (!_statusItems.TryGetValue(id, out var item))
        {
            if (_statusItems.Count >= 10) throw new JsHostException("A plugin can have at most 10 status items.");
            _statusItems[id] = item = _host!.CreateStatusItem(id);
        }
        item.Update(Truncate(text, 80), Enum.TryParse<StatusLevel>(level, ignoreCase: true, out var parsed) ? parsed : StatusLevel.Idle);
    }

    private void Hotkey(string combo)
    {
        Require(JsPermissions.Input);
        if (!HotkeyParser.TryParse(combo, out var parsed, out var error))
            throw new JsHostException($"Not a valid key combination: {error}");
        (_input ?? throw new JsHostException("Keyboard input is not available.")).SendKeyCombo(parsed);
    }

    private void TypeText(string text)
    {
        Require(JsPermissions.Input);
        (_input ?? throw new JsHostException("Keyboard input is not available.")).TypeText(Truncate(text, 2000));
    }

    private string Http(string method, string url, string body, string headersJson)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new JsHostException("Not a valid URL.");
        if (!_permissions.AllowsHttp(uri))
            throw new JsHostException($"This plugin has not been granted 'http:{uri.Host}:{uri.Port}'.");

        using var request = new HttpRequestMessage(new HttpMethod(method), uri);
        if (method != "GET") request.Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        foreach (var (key, value) in JsonSerializer.Deserialize<Dictionary<string, string>>(headersJson) ?? [])
            request.Headers.TryAddWithoutValidation(key, value);

        try
        {
            using var response = _http.Send(request);
            using var stream = response.Content.ReadAsStream();
            var buffer = new byte[_limits.MaxHttpResponseBytes + 1];
            var read = 0;
            int n;
            while (read < buffer.Length && (n = stream.Read(buffer, read, buffer.Length - read)) > 0) read += n;
            if (read > _limits.MaxHttpResponseBytes) throw new JsHostException("The response is too large.");
            return JsonSerializer.Serialize(new { status = (int)response.StatusCode, body = System.Text.Encoding.UTF8.GetString(buffer, 0, read) });
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            throw new JsHostException($"The request failed: {ex.Message}");
        }
    }

    private void StartTimer(int id, int intervalMs, bool repeat)
    {
        if (intervalMs < 100) throw new JsHostException("The shortest timer is 100 ms.");
        lock (_timerLock)
        {
            if (_disposed) return;
            if (_timers.Count >= _limits.MaxTimers) throw new JsHostException($"A plugin can have at most {_limits.MaxTimers} timers.");
            var timer = new Timer(_ => FireTimer(id, repeat), null, intervalMs, repeat ? intervalMs : Timeout.Infinite);
            _timers[id] = timer;
        }
    }

    private void CancelTimer(int id)
    {
        lock (_timerLock)
        {
            if (_timers.Remove(id, out var timer)) timer.Dispose();
        }
    }

    private void FireTimer(int id, bool repeat)
    {
        // A busy plugin must not pile up ticks: extra ones are dropped, not queued.
        if (Interlocked.Increment(ref _pendingTimerJobs) > MaxPendingTimerJobs)
        {
            Interlocked.Decrement(ref _pendingTimerJobs);
            return;
        }
        if (!repeat) CancelTimer(id);

        _ = Post(() =>
        {
            try { Invoke("__fire", id); }
            catch (InvalidOperationException) { /* already logged and counted */ }
            finally { Interlocked.Decrement(ref _pendingTimerJobs); }
            return 0;
        });
    }

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..max];

    [GeneratedRegex(@"^[A-Za-z0-9._\-]+$")]
    private static partial Regex VariableName();

    /// <summary>Publishes the catalog entries the script described, so the editor's variable picker lists them.</summary>
    private sealed class DescriptionProvider(List<VariableInfo> infos) : IVariableProvider, IVariableCatalogSource
    {
        public IEnumerable<VariableInfo> Describe() => infos;
        public Task RunAsync(IVariableStore store, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
