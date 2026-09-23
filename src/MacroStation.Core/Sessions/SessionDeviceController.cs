using MacroStation.Core.Profiles;
using MacroStation.Plugin.Abstractions;
using MacroStation.Protocol;

namespace MacroStation.Core.Sessions;

/// <summary>
/// <see cref="IDeviceController"/> for one connected client: what "next/prev/back page" and
/// "switch profile" mean for that specific phone, independent of every other connected device.
/// </summary>
public sealed class SessionDeviceController(ClientSession session, ProfileStore profiles, WidgetStateService widgetState) : IDeviceController
{
    public Task ShowPageAsync(string pageId) => NavigateAsync(pageId, pushHistory: true);

    public Task NextPageAsync() => NavigateRelativeAsync(+1);

    public Task PreviousPageAsync() => NavigateRelativeAsync(-1);

    public Task BackAsync() =>
        session.PageHistory.Count > 0 ? NavigateAsync(session.PageHistory.Pop(), pushHistory: false) : Task.CompletedTask;

    /// <summary>A plain user pick — drawer or a <c>core.profile</c> button. Records the switch as this
    /// session's new auto-switch base (docs/auto-profile-switch.md's "Elle seçim") before applying it.
    /// <see cref="Sessions.AutoProfileSwitcher"/> does not call this overload: it updates
    /// <see cref="ClientSession.AutoSwitch"/> itself (Auto origin) and calls <see cref="ApplyProfileAsync"/>
    /// directly, so a rule match is never also recorded as a manual pick.</summary>
    public Task SwitchProfileAsync(string profileId)
    {
        session.AutoSwitch.OnManual(profileId);
        return ApplyProfileAsync(profileId);
    }

    /// <summary>The actual "push this profile's layout to the device" work, with no auto-switch stack
    /// side effect — shared by the public Manual entry point above and by
    /// <see cref="Sessions.AutoProfileSwitcher"/>, which manages the stack itself.</summary>
    internal async Task ApplyProfileAsync(string profileId)
    {
        var profile = profiles.Get(profileId);
        var page = profile?.Pages.FirstOrDefault();
        if (profile is null || page is null) return;

        session.ProfileId = profileId;
        session.PageId = page.Id;
        session.PageHistory.Clear();
        session.SentTexts.Clear();
        session.SentStyles.Clear();
        session.SentValues.Clear();

        await session.SendAsync(MessageTypes.LayoutFull, new LayoutFullPayload(profile, page.Id));
        await widgetState.SendInitialAsync(session, page, CancellationToken.None);
    }

    private Task NavigateRelativeAsync(int delta)
    {
        var profile = profiles.Get(session.ProfileId ?? "");
        if (profile is null || profile.Pages.Count == 0) return Task.CompletedTask;

        var index = profile.Pages.FindIndex(p => p.Id == session.PageId);
        if (index < 0) return Task.CompletedTask;

        // Wraps around: last page's "next" goes to the first page and vice versa.
        var nextIndex = ((index + delta) % profile.Pages.Count + profile.Pages.Count) % profile.Pages.Count;
        return NavigateAsync(profile.Pages[nextIndex].Id, pushHistory: true);
    }

    private async Task NavigateAsync(string pageId, bool pushHistory)
    {
        var profile = profiles.Get(session.ProfileId ?? "");
        var page = profile?.FindPage(pageId);
        if (page is null) return;

        if (pushHistory && session.PageId is not null && session.PageId != pageId)
            session.PageHistory.Push(session.PageId);

        session.PageId = pageId;
        await session.SendAsync(MessageTypes.PageShow, new PageShowMessage(pageId));
        await widgetState.SendInitialAsync(session, page, CancellationToken.None);
    }
}
