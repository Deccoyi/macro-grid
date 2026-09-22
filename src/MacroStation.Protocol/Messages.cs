namespace MacroStation.Protocol;

// ---- client -> server ----
public sealed record HelloMessage(string DeviceId, string DeviceName, string? Token, string ClientVersion);
public sealed record WidgetEventMessage(string PageId, string WidgetId);
public sealed record WidgetValueMessage(string PageId, string WidgetId, double Value);
public sealed record PageChangeMessage(string PageId);

// ---- server -> client ----
public sealed record WelcomeMessage(string ServerName, string ServerVersion);
public sealed record PageShowMessage(string PageId);
/// <summary><paramref name="Style"/> is property name ("background"/"foreground"/"borderColor") to resolved CSS value, from dynamized properties.</summary>
public sealed record WidgetStateMessage(
    string WidgetId,
    string? Text = null,
    double? Value = null,
    bool? Active = null,
    Dictionary<string, string>? Style = null);
public sealed record ErrorMessage(string Code, string Message);
