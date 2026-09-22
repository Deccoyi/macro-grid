namespace MacroStation.Plugin.Abstractions;

/// <summary>
/// What "change page" or "switch profile" means for the single client an action was triggered from.
/// Each connected client gets its own controller, so a page/profile change on one phone never affects another.
/// </summary>
public interface IDeviceController
{
    Task ShowPageAsync(string pageId);

    Task NextPageAsync();

    Task PreviousPageAsync();

    /// <summary>Returns to the previously shown page, if any. A no-op when the history is empty.</summary>
    Task BackAsync();

    Task SwitchProfileAsync(string profileId);
}
