using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using MacroGrid.Core.Diagnostics;
using Microsoft.Extensions.Logging;
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

    /// <summary>The rules every keyboard call passes first: the permission, a real button press that is still open, not an
    /// administrator server, and a window in front that may receive input. Returns the open press window.</summary>
    private JsPressWindow RequireInput()
    {
        Require(JsPermissions.Input);
        if (_inputBlocked)
            throw new JsHostException("Keyboard input is switched off for this plugin.");
        if (_press is not { Settled: false } press || Stopwatch.GetTimestamp() > press.Deadline)
            throw new JsHostException("Keyboard input is only allowed while handling a button press.");
        if (_input is null)
            throw new JsHostException("Keyboard input is not available.");
        if (_isElevated())
            throw new JsHostException("Keyboard input is not available while Macro Grid runs as administrator.");
        if (JsInputPolicy.RefuseTarget(_windows?.GetForeground()) is { } refused)
            throw new JsHostException(refused);
        return press;
    }

    /// <summary>Runs one keyboard call. A refusal is put on the problem list (with the reason, counted when it repeats) so the person can
    /// see why a button did nothing, then thrown to the script as before.</summary>
    private void GuardInput(string what, Action call)
    {
        try { call(); }
        catch (JsHostException ex)
        {
            if (!_inputBlocked)
            {
                _lastReported = ex.Message;
                _problems?.Report(_manifest.Id, _manifest.Name, ProblemSeverity.Warning, ProblemCodes.InputRefused, $"{what}: {ex.Message}");
            }
            throw;
        }
    }

    private void Hotkey(string combo) => GuardInput("Key combination refused", () => HotkeyCore(combo));

    private void TypeText(string text) => GuardInput("Typing refused", () => TypeTextCore(text));

    private void HotkeyCore(string combo)
    {
        var press = RequireInput();
        if (!HotkeyParser.TryParse(combo, out var parsed, out var error))
            throw new JsHostException($"Not a valid key combination: {error}");
        if (JsInputPolicy.RefuseCombo(parsed) is { } refused)
            throw new JsHostException(refused);
        if (press.KeyCombos >= JsInputPolicy.MaxKeyCombosPerPress)
            throw new JsHostException($"At most {JsInputPolicy.MaxKeyCombosPerPress} key combinations can be pressed in one button press.");

        press.KeyCombos++;
        CountKeyboardUse(press, 1);
        _input!.SendKeyCombo(parsed);
    }

    private void TypeTextCore(string text)
    {
        var press = RequireInput();
        if (text.Length > JsInputPolicy.MaxCharsPerTypeCall)
            throw new JsHostException($"host.input.type takes at most {JsInputPolicy.MaxCharsPerTypeCall} characters.");
        if (press.TypedChars + text.Length > JsInputPolicy.MaxTypedCharsPerPress)
            throw new JsHostException($"At most {JsInputPolicy.MaxTypedCharsPerPress} characters can be typed in one button press.");

        if (JsInputPolicy.MatchBlockedText(JsInputPolicy.Normalize(press.Text + text)) is { } rule)
        {
            // Never log the text, only which rule matched.
            _inputBlocked = true;
            _logger.LogWarning(SecurityEvents.PluginInputBlocked, "Security: plugin {Id} tried to type a blocked command (rule {Rule})",
                SecurityEvents.ForLog(_manifest.Id), rule);
            _lastReported = "This text is not allowed.";
            _problems?.Report(_manifest.Id, _manifest.Name, ProblemSeverity.Error, ProblemCodes.InputBlocked, "Typing refused: it tried to type a blocked command, so the plugin was switched off.");
            _onFaulted("Switched off: it tried to type a blocked command.");
            throw new JsHostException("This text is not allowed.");
        }

        press.TypedChars += text.Length;
        press.Text.Append(text);
        CountKeyboardUse(press, text.Length);
        _input!.TypeText(text);
    }

    /// <summary>Counts a press that used the keyboard once a day and logs how many keys it sent (never which).</summary>
    private void CountKeyboardUse(JsPressWindow press, int keys)
    {
        if (!press.Counted)
        {
            press.Counted = true;
            lock (_usageLock)
            {
                var today = DateOnly.FromDateTime(DateTime.Now);
                if (_usageDay != today) { _usageDay = today; _usesToday = 0; }
                _usesToday++;
            }
        }
        _logger.LogInformation("Plugin {Id} sent {Keys} key(s) in a button press ({Combos} combinations, {Chars} characters so far)",
            SecurityEvents.ForLog(_manifest.Id), keys, press.KeyCombos, press.TypedChars);
    }

    /// <summary>Checks the URL against the approved host:port pairs and builds the request; both the blocking and
    /// the async call go through here so they can never differ in what they allow.</summary>
    private HttpRequestMessage BuildRequest(string method, string url, string body, string headersJson)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new JsHostException("Not a valid URL.");
        if (!_permissions.AllowsHttp(uri))
            throw new JsHostException($"This plugin has not been granted 'http:{uri.Host}:{uri.Port}'.");

        var request = new HttpRequestMessage(new HttpMethod(method), uri);
        if (method != "GET") request.Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        foreach (var (key, value) in JsonSerializer.Deserialize<Dictionary<string, string>>(headersJson) ?? [])
        {
            if (JsNetworkGuard.IsRefusedHeader(key) || JsNetworkGuard.IsMalformedHeader(key, value))
            {
                request.Dispose();
                ReportNetworkRefused(uri.Host, uri.Port, "A request header is not allowed.");
                throw new JsHostException(JsNetworkGuard.IsRefusedHeader(key) ? $"The header '{key}' cannot be set." : "A request header is not valid.");
            }
            request.Headers.TryAddWithoutValidation(key, value);
        }
        return request;
    }

    /// <summary>One Error List line and one security log line for a network call the rules refused (never the URL path, a header or a body).</summary>
    private void ReportNetworkRefused(string host, int port, string reason)
    {
        _problems?.Report(_manifest.Id, _manifest.Name, ProblemSeverity.Warning, ProblemCodes.NetworkRefused, $"A network call to {host}:{port} was refused: {reason}");
        _logger.LogWarning(SecurityEvents.PluginNetworkRefused, "Security: a network call of plugin {Id} to {Host}:{Port} was refused",
            SecurityEvents.ForLog(_manifest.Id), SecurityEvents.ForLog(host), port);
    }

    /// <summary>The text of a failed request: the refusal's own words when the network guard stopped it, else the framework's.</summary>
    private static string DescribeFailure(Exception ex) => (ex.InnerException as NetworkRefusedException ?? ex as NetworkRefusedException)?.Message ?? ex.Message;

    private string Http(string method, string url, string body, string headersJson)
    {
        using var request = BuildRequest(method, url, body, headersJson);
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
            throw new JsHostException($"The request failed: {DescribeFailure(ex)}");
        }
    }

    /// <summary>Starts a request without blocking the plugin thread: the script keeps running (timers, actions)
    /// while it is in flight, and the outcome comes back to the plugin thread as one more job that settles the
    /// promise the script holds. Same permission, timeout, redirect and size rules as <see cref="Http"/>; at most
    /// <see cref="JsPluginLimits.MaxPendingHttp"/> requests may be in flight at once.</summary>
    private void HttpAsync(int id, string method, string url, string body, string headersJson)
    {
        // An answer that belongs to a request started during a button press continues that press (until its window closes).
        var press = _press;
        var request = BuildRequest(method, url, body, headersJson);
        if (Interlocked.Increment(ref _pendingHttp) > _limits.MaxPendingHttp)
        {
            Interlocked.Decrement(ref _pendingHttp);
            request.Dispose();
            throw new JsHostException($"A plugin can have at most {_limits.MaxPendingHttp} requests in flight.");
        }

        _ = Task.Run(async () =>
        {
            bool ok;
            string payload;
            try
            {
                using (request)
                {
                    using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, _disposeCts.Token);
                    await using var stream = await response.Content.ReadAsStreamAsync(_disposeCts.Token);
                    var buffer = new byte[_limits.MaxHttpResponseBytes + 1];
                    var read = 0;
                    int n;
                    while (read < buffer.Length && (n = await stream.ReadAsync(buffer.AsMemory(read, buffer.Length - read), _disposeCts.Token)) > 0) read += n;
                    if (read > _limits.MaxHttpResponseBytes) throw new JsHostException("The response is too large.");
                    payload = JsonSerializer.Serialize(new { status = (int)response.StatusCode, body = System.Text.Encoding.UTF8.GetString(buffer, 0, read) });
                    ok = true;
                }
            }
            catch (JsHostException ex) { ok = false; payload = ex.Message; }
            catch (Exception ex) when (ex is HttpRequestException or IOException or OperationCanceledException)
            {
                if (_disposed) return;
                ok = false;
                payload = $"The request failed: {DescribeFailure(ex)}";
            }
            finally { Interlocked.Decrement(ref _pendingHttp); }

            _ = Post(() =>
            {
                _press = press;
                try { Invoke("__httpDone", id, ok, payload); }
                catch (InvalidOperationException) { /* already logged and counted */ }
                finally { _press = null; }
                return 0;
            });
        });
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
