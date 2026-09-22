namespace MacroStation.Protocol;

// ---- client -> server ----
public sealed record HelloMessage(string DeviceId, string DeviceName, string? Token, string ClientVersion);
public sealed record WidgetEventMessage(string PageId, string WidgetId);
public sealed record WidgetValueMessage(string PageId, string WidgetId, double Value);
public sealed record PageChangeMessage(string PageId);
public sealed record ProfileChangeMessage(string ProfileId);

// ---- server -> client ----
public sealed record WelcomeMessage(string ServerName, string ServerVersion);
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
