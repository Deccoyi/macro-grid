using System.Net.WebSockets;
using MacroGrid.Core.Model;
using MacroGrid.Core.Profiles;
using MacroGrid.Core.Sessions;
using MacroGrid.Core.Variables;
using MacroGrid.Core.Widgets;
using MacroGrid.Plugin.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;

namespace MacroGrid.Tests;

/// <summary>What <see cref="IPluginHost.Demand"/> answers: the default for an old host, the host's sources, leases and the change event.</summary>
public sealed class VariableDemandTests : IDisposable
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Source(params string[] names) : IVariableDemandSource
    {
        public bool Observes(string name) => names.Contains(name);
        public bool ObservesPrefix(string prefix) => names.Any(n => n.StartsWith(prefix, StringComparison.Ordinal));
    }

    private sealed class Broken : IVariableDemandSource
    {
        public bool Observes(string name) => throw new InvalidOperationException();
        public bool ObservesPrefix(string prefix) => throw new InvalidOperationException();
    }

    private sealed class Socket : WebSocket
    {
        public override WebSocketState State => WebSocketState.Open;
        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override string? SubProtocol => null;
        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken) => Task.CompletedTask;
        public override ValueTask SendAsync(ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken) => ValueTask.CompletedTask;
        public override void Abort() { }
        public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) => Task.CompletedTask;
        public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) => Task.CompletedTask;
        public override void Dispose() { }
        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    /// <summary>A host written before Demand existed: it implements only what the 1.0.0 interface required.</summary>
    private sealed class OldHost : IPluginHost
    {
        public string ServerVersion => "1.0.0";
        public string SdkVersion => "1.0.0";
        public string DataDirectory => "";
        public void Log(string message) { }
        public void RegisterAction(IActionHandler handler) { }
        public void RegisterVariableProvider(IVariableProvider provider) { }
        public void RegisterSettingsPage(IPluginSettingsPage page) { }
        public IPluginStatusItem CreateStatusItem(string id) => throw new NotSupportedException();
        public void RegisterIconPack(IIconPackSource iconPack) { }
        public IPluginSecrets Secrets => throw new NotSupportedException();
        public IPluginWidgets Widgets => throw new NotSupportedException();
    }

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "mg-demand-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void A_host_without_demand_information_says_everything_is_needed()
    {
        IPluginHost host = new OldHost();

        Assert.True(host.Demand.IsObserved("obs.stream.timecode"));
        Assert.True(host.Demand.IsPrefixObserved("obs."));
        host.Demand.Changed += () => { };
    }

    [Fact]
    public void Nothing_is_needed_without_a_reader()
    {
        var demand = new VariableDemand();

        Assert.False(demand.IsObserved("obs.stream.timecode"));
        Assert.False(demand.IsPrefixObserved("obs."));
    }

    [Fact]
    public void A_source_answers_for_names_and_prefixes()
    {
        var demand = new VariableDemand();
        demand.AddSource(new Source("obs.stream.timecode"));

        Assert.True(demand.IsObserved("obs.stream.timecode"));
        Assert.False(demand.IsObserved("obs.stream.duration"));
        Assert.True(demand.IsPrefixObserved("obs.stream."));
        Assert.False(demand.IsPrefixObserved("obs.record."));
    }

    [Fact]
    public void A_failing_source_counts_as_needed()
    {
        var demand = new VariableDemand();
        demand.AddSource(new Broken());

        Assert.True(demand.IsObserved("a.b"));
        Assert.True(demand.IsPrefixObserved("a."));
    }

    [Fact]
    public void A_read_keeps_a_variable_needed_for_the_lease_and_raises_changed_once()
    {
        var clock = new Clock();
        var demand = new VariableDemand(clock);
        var raised = 0;
        demand.Changed += () => raised++;

        demand.Touch("obs.fps");
        demand.Touch("obs.fps");

        Assert.Equal(1, raised);
        Assert.True(demand.IsObserved("obs.fps"));
        Assert.True(demand.IsPrefixObserved("obs."));
        Assert.False(demand.IsObserved("obs.other"));

        clock.Now += VariableDemand.LeaseDuration + TimeSpan.FromSeconds(1);
        Assert.False(demand.IsObserved("obs.fps"));
    }

    [Fact]
    public void A_snapshot_makes_every_variable_needed_for_the_lease()
    {
        var clock = new Clock();
        var demand = new VariableDemand(clock);

        demand.TouchAll();
        Assert.True(demand.IsObserved("anything.at.all"));

        clock.Now += VariableDemand.LeaseDuration + TimeSpan.FromSeconds(1);
        Assert.False(demand.IsObserved("anything.at.all"));
    }

    [Fact]
    public void A_handler_that_throws_does_not_stop_the_others()
    {
        var demand = new VariableDemand();
        var reached = false;
        demand.Changed += () => throw new InvalidOperationException();
        demand.Changed += () => reached = true;

        demand.NotifyChanged();

        Assert.True(reached);
    }

    [Fact]
    public void The_store_snapshot_counts_as_demand()
    {
        var store = new VariableStore();

        Assert.False(store.Demand.IsObserved("x.y"));
        store.Snapshot();
        Assert.True(store.Demand.IsObserved("x.y"));
    }

    [Fact]
    public void A_plugin_reading_its_own_variable_does_not_make_it_needed_but_reading_another_does()
    {
        var store = new VariableStore();
        var tracking = new TrackingVariableStore(store);

        tracking.Set("mine.value", 1);
        tracking.Get("mine.value");
        tracking.Get("other.value");

        Assert.False(store.Demand.IsObserved("mine.value"));
        Assert.True(store.Demand.IsObserved("other.value"));
    }

    [Fact]
    public void A_widget_on_a_shown_page_makes_its_variable_needed_and_a_hidden_page_does_not()
    {
        var profiles = new ProfileStore(_dir);
        profiles.Save(new Profile
        {
            Id = "p", Name = "P",
            Pages =
            [
                new Page { Id = "a", Widgets = [new Widget { Id = "w1", Text = "{obs.stream.timecode}" }] },
                new Page { Id = "b", Widgets = [new Widget { Id = "w2", Text = "{obs.record.timecode}" }] },
            ],
        });
        var variables = new VariableStore();
        var sessions = new SessionRegistry();
        using var service = new WidgetStateService(variables, sessions, profiles, new ToggleStateStore(), new LayoutSender(new AssetStore()), NullLogger<WidgetStateService>.Instance);

        Assert.False(variables.Demand.IsObserved("obs.stream.timecode"));

        sessions.Add(new ClientSession(new Socket()) { DeviceId = "d", ProfileId = "p", PageId = "a" });

        Assert.True(variables.Demand.IsObserved("obs.stream.timecode"));
        Assert.True(variables.Demand.IsPrefixObserved("obs.stream."));
        Assert.False(variables.Demand.IsObserved("obs.record.timecode"));
        Assert.False(variables.Demand.IsPrefixObserved("obs.record."));
    }
}
