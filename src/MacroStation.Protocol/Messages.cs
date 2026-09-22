namespace MacroStation.Protocol;

// ---- client -> server ----
/// <summary><paramref name="Pin"/> is only needed the first time (or after a re-pair): a device that
/// already holds a valid <paramref name="Token"/> from a previous pairing never needs to send it.</summary>
public sealed record HelloMessage(string DeviceId, string DeviceName, string? Token, string ClientVersion, string? Pin = null);
public sealed record WidgetEventMessage(string PageId, string WidgetId);
public sealed record WidgetValueMessage(string PageId, string WidgetId, double Value);
public sealed record PageChangeMessage(string PageId);
public sealed record ProfileChangeMessage(string ProfileId);

// ---- server -> client ----
/// <summary><paramref name="Token"/> is only present when a NEW pairing just happened this hello — the
/// client must save it (it's what replaces the PIN on every future connection).</summary>
public sealed record WelcomeMessage(string ServerName, string ServerVersion, string? Token = null);
public sealed record PageShowMessage(string PageId);
public sealed record ProfileSummary(string Id, string Name);
/// <summary>Every profile on the server, sent on hello so a client can offer a profile-switcher (drawer) — not just the one it's currently assigned to.</summary>
public sealed record ProfilesListPayload(List<ProfileSummary> Profiles);
/// <summary><paramref name="Style"/> is property name ("background"/"foreground"/"borderColor") to resolved CSS value, from dynamized properties.</summary>
public sealed record WidgetStateMessage(
    string WidgetId,
    string? Text = null,
    double? Value = null,
    bool? Active = null,
    Dictionary<string, string>? Style = null);
public sealed record ErrorMessage(string Code, string Message);
