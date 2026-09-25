using MacroGrid.Core.Model;
using MacroGrid.Core.Preferences;

namespace MacroGrid.Core.Profiles;

/// <summary>Resolves which profile a device opens when nothing more specific applies — the empty-stack
/// end of docs/design/auto-profile-switch.md's fallback chain, and also the plain (pre-auto-switch) default a
/// device without <c>AssignedProfileId</c> always used. One place so <c>ClientHub.OnHelloAsync</c> and
/// <see cref="Sessions.AutoProfileSwitcher"/> agree on the same chain.</summary>
public static class ProfileResolver
{
    /// <summary>Device's own assignment, then the app-wide default, then just the first profile.</summary>
    public static Profile ResolveDefault(PairedDevice device, ProfileStore profiles, PreferencesStore preferences)
    {
        if (device.AssignedProfileId is { } assignedId && profiles.Get(assignedId) is { } assigned)
            return assigned;

        if (preferences.Get().DefaultProfileId is { } defaultId && profiles.Get(defaultId) is { } byDefault)
            return byDefault;

        return profiles.First();
    }
}
