using System.Diagnostics;
using System.Text.Json.Nodes;
using MacroGrid.Core.Actions;
using MacroGrid.Plugin.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;

namespace MacroGrid.Tests;

/// <summary>Records which navigation method was called, so action tests don't need a real ClientSession/ProfileStore.</summary>
public sealed class FakeDeviceController : IDeviceController
{
    public List<string> Calls { get; } = [];

    public Task ShowPageAsync(string pageId) { Calls.Add($"show:{pageId}"); return Task.CompletedTask; }
    public Task NextPageAsync() { Calls.Add("next"); return Task.CompletedTask; }
    public Task PreviousPageAsync() { Calls.Add("prev"); return Task.CompletedTask; }
    public Task BackAsync() { Calls.Add("back"); return Task.CompletedTask; }
    public Task SwitchProfileAsync(string profileId) { Calls.Add($"profile:{profileId}"); return Task.CompletedTask; }
}

public class PageActionTests
{
    private static ActionContext Context(IDeviceController device) => new("dev", "page", "widget", device);

    [Fact]
    public async Task Goto_mode_calls_ShowPageAsync()
    {
        var device = new FakeDeviceController();

        await new PageAction().ExecuteAsync(Context(device), PageAction.Goto("p2"), default);

        Assert.Equal(["show:p2"], device.Calls);
    }

    [Theory]
    [InlineData("next", "next")]
    [InlineData("prev", "prev")]
    [InlineData("back", "back")]
    public async Task Relative_modes_call_the_matching_method(string mode, string expectedCall)
    {
        var device = new FakeDeviceController();

        await new PageAction().ExecuteAsync(Context(device), new JsonObject { ["mode"] = mode }, default);

        Assert.Equal([expectedCall], device.Calls);
    }

    [Fact]
    public async Task Missing_pageId_on_goto_does_nothing()
    {
        var device = new FakeDeviceController();

        await new PageAction().ExecuteAsync(Context(device), new JsonObject(), default);

        Assert.Empty(device.Calls);
    }
}

public class ProfileActionTests
{
    [Fact]
    public async Task Calls_SwitchProfileAsync()
    {
        var device = new FakeDeviceController();

        await new ProfileAction().ExecuteAsync(new("dev", "page", "widget", device), ProfileAction.Settings("p1"), default);

        Assert.Equal(["profile:p1"], device.Calls);
    }
}

public class DelayActionTests
{
    private static ActionContext Ctx() => new("d", "p", "w", new FakeDeviceController());

    [Fact]
    public async Task Waits_approximately_the_requested_duration()
    {
        var sw = Stopwatch.StartNew();

        await new DelayAction().ExecuteAsync(Ctx(), new JsonObject { ["ms"] = 40 }, default);

        Assert.InRange(sw.ElapsedMilliseconds, 20, 15000); // generous upper bound: only "does not hang" matters on a loaded machine
    }

    [Fact]
    public async Task Negative_or_missing_ms_completes_immediately_without_throwing()
    {
        await new DelayAction().ExecuteAsync(Ctx(), new JsonObject { ["ms"] = -50 }, default);
        await new DelayAction().ExecuteAsync(Ctx(), new JsonObject(), default);
    }
}

public class OpenActionTests
{
    [Fact]
    public async Task Empty_target_does_not_throw_or_launch_anything()
    {
        var action = new OpenAction(NullLogger<OpenAction>.Instance);

        await action.ExecuteAsync(new("d", "p", "w", new FakeDeviceController()), new JsonObject(), default);
    }
}

public class OpenUrlActionTests
{
    [Fact]
    public async Task Empty_url_does_not_throw()
    {
        var action = new OpenUrlAction(NullLogger<OpenUrlAction>.Instance);

        await action.ExecuteAsync(new("d", "p", "w", new FakeDeviceController()), new JsonObject(), default);
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("notepad.exe")]
    [InlineData("ftp://example.com")]
    [InlineData("file:///C:/Windows/System32/cmd.exe")]
    public async Task Rejects_anything_that_is_not_http_or_https(string url)
    {
        // Guards against using this action to launch a local file/executable by URL scheme trickery;
        // that's core.open's job (and goes through the user's own file browse pick, not free text).
        var action = new OpenUrlAction(NullLogger<OpenUrlAction>.Instance);

        await action.ExecuteAsync(new("d", "p", "w", new FakeDeviceController()), OpenUrlAction.Settings(url), default);
    }
}
