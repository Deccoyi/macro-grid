using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Core.Actions;

/// <summary>The device of a run that no device started (an automation rule on a time or a value): every call fails with a text that says why, so a page or profile step is a visible failure, not a silent skip.</summary>
public sealed class NoDeviceController : IDeviceController
{
    public const string Text = "This step needs a device. A rule that is not started by a device cannot change pages or profiles.";

    public Task ShowPageAsync(string pageId) => throw new InvalidOperationException(Text);
    public Task NextPageAsync() => throw new InvalidOperationException(Text);
    public Task PreviousPageAsync() => throw new InvalidOperationException(Text);
    public Task BackAsync() => throw new InvalidOperationException(Text);
    public Task SwitchProfileAsync(string profileId) => throw new InvalidOperationException(Text);
}
