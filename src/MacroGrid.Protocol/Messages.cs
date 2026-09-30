using System.Text.Json.Nodes;

namespace MacroGrid.Protocol;

// ---- client -> server ----
/// <summary><paramref name="Pin"/> is only needed the first time (or after a re-pair): a device that
/// already holds a valid <paramref name="Token"/> from a previous pairing never needs to send it.
/// <paramref name="Capabilities"/> lists the optional protocol features the client understands (see
/// <see cref="ClientCapabilities"/>); a client that sends none keeps getting the plain full layout with
/// icons inlined.</summary>
public sealed record HelloMessage(string DeviceId, string DeviceName, string? Token, string ClientVersion, string? Pin = null, string[]? Capabilities = null);

/// <summary>Optional protocol features a client announces in <see cref="HelloMessage.Capabilities"/>.</summary>
public static class ClientCapabilities
{
    /// <summary>Large <c>data:</c> values (icons, images) in a layout are replaced by <c>asset:&lt;hash&gt;</c>
    /// references; the client fetches each one once with <c>asset.get</c> and caches it.</summary>
    public const string Assets = "assets";

    /// <summary>A saved profile edit is sent as a <c>layout.patch</c> (only the changed widgets) instead of a full layout.</summary>
    public const string LayoutPatch = "layout.patch";

    /// <summary>The client can run custom plugin widgets (sandboxed workers drawing to a canvas). A widget's script and images arrive as asset references
    /// the client fetches with <c>asset.get</c> (which every client may use, whether or not it announced <see cref="Assets"/>). Without this capability a plugin widget
    /// is sent without its code and the client draws a placeholder.</summary>
    public const string PluginWidgets = "plugin-widgets";
}

/// <summary>Client asks for the data of asset references it does not have cached yet.</summary>
public sealed record AssetGetMessage(string[] Hashes);

/// <summary>One asset's content. <paramref name="Data"/> (a <c>data:</c> URI) is null when the server no longer
/// knows that hash, so the client can stop waiting for it.</summary>
public sealed record AssetMessage(string Hash, string? Data);
public sealed record WidgetEventMessage(string PageId, string WidgetId);
public sealed record WidgetValueMessage(string PageId, string WidgetId, double Value);
public sealed record PageChangeMessage(string PageId);
public sealed record ProfileChangeMessage(string ProfileId);
/// <summary>The drawer's auto-switch pause toggle (docs/design/auto-profile-switch.md). A no-op for a device
/// that doesn't have <c>FollowActiveWindow</c> on — there's nothing to lock.</summary>
public sealed record ProfileLockMessage(bool Locked);

// ---- server -> client ----
/// <summary><paramref name="Token"/> is only present when a NEW pairing just happened this hello — the
/// client must save it (it's what replaces the PIN on every future connection).</summary>
public sealed record WelcomeMessage(string ServerName, string ServerVersion, string? Token = null);
public sealed record PageShowMessage(string PageId);
public sealed record ProfileSummary(string Id, string Name);
/// <summary>This device's auto-profile-switch opt-in and current lock state (docs/design/auto-profile-switch.md).
/// <paramref name="Enabled"/> false means the device never auto-switches — the client can hide the lock
/// control entirely in that case, since there's nothing to pause.</summary>
public sealed record AutoSwitchInfo(bool Enabled, bool Locked);
/// <summary>Every profile on the server, sent on hello so a client can offer a profile-switcher (drawer) —
/// not just the one it's currently assigned to.</summary>
public sealed record ProfilesListPayload(List<ProfileSummary> Profiles, AutoSwitchInfo? AutoSwitch = null);
/// <summary><paramref name="Style"/> is property name ("background"/"foreground"/"borderColor") to resolved CSS value, from dynamized properties.
/// <paramref name="Url"/> is for a <c>web</c> widget only: the address this device shows instead of the profile's (set by a button), or an empty string
/// to go back to the profile's. <paramref name="Reload"/> is a counter for a <c>web</c> widget; a higher number than before means "load the page again".
/// A client that does not know these two fields ignores them.</summary>
public sealed record WidgetStateMessage(
    string WidgetId,
    string? Text = null,
    double? Value = null,
    bool? Active = null,
    Dictionary<string, string>? Style = null,
    string? Url = null,
    int? Reload = null);
/// <summary><paramref name="RetryAfterSeconds"/> is set only for <c>pairing_required</c> when the address is
/// blocked after too many wrong PINs — a machine-readable seconds-to-wait a client can count down with, kept
/// separate from <paramref name="Message"/> so the prose text can change (wording, translation) without
/// breaking a client that reads this field instead of parsing the sentence for a number. <paramref name="Reason"/>
/// is likewise set only for <c>pairing_required</c>: one of <c>"wrong_pin"</c>, <c>"locked_out"</c>,
/// <c>"pairing_closed"</c>, <c>"not_paired"</c> — a stable code so a client can show its own localized text
/// instead of <paramref name="Message"/>, which is always English prose. A client that doesn't recognize a
/// <paramref name="Reason"/> (an older build against a newer server that adds a case) falls back to
/// <paramref name="Message"/>.</summary>
public sealed record ErrorMessage(string Code, string Message, int? RetryAfterSeconds = null, string? Reason = null);

// ---- plugin widgets (capability "plugin-widgets") ----

/// <summary>What a plugin widget asks of the server. <paramref name="Kind"/> is one of <see cref="PluginWidgetKinds"/>. The message carries no plugin id:
/// the server takes it from the widget in the profile this device has open.</summary>
/// <param name="Id">A positive number chosen by the client for <c>request</c> and <c>run</c>; the reply carries it back.</param>
/// <param name="Data"><c>request</c>: what the widget sent (at most 16 KB). <c>run</c>: <c>{ action, settings }</c>. <c>subscribe</c>: <c>{ variables: string[] }</c> (at most 32).</param>
/// <param name="UserGesture">For <c>run</c>: true only when the client's renderer saw a real touch on this widget a moment ago. Only then may a keyboard-using plugin action type.</param>
public sealed record PluginWidgetRequestMessage(string PageId, string WidgetId, string Kind, int? Id = null, JsonNode? Data = null, bool UserGesture = false);

public static class PluginWidgetKinds
{
    /// <summary>The widget's code is running; the server answers with the widget's retained events and current values.</summary>
    public const string Ready = "ready";
    public const string Request = "request";
    public const string Run = "run";
    public const string Subscribe = "subscribe";
}

/// <summary>The answer to a <c>request</c> or <c>run</c>. <paramref name="Error"/> is a short code: <c>rate_limited</c>, <c>too_large</c>, <c>not_allowed</c>,
/// <c>plugin_unavailable</c>, <c>timeout</c>, <c>failed</c>, <c>bad_request</c>.</summary>
public sealed record PluginWidgetReplyMessage(string WidgetId, int Id, bool Ok, JsonNode? Data = null, string? Error = null, string? Message = null);

/// <summary>Changed values of the variables a widget subscribed to (the current values right after a subscribe).</summary>
public sealed record PluginWidgetVarsMessage(string WidgetId, Dictionary<string, JsonNode?> Values);

/// <summary>An event the widget's plugin pushed.</summary>
public sealed record PluginWidgetEventMessage(string WidgetId, string Name, JsonNode? Data);

/// <summary>An error from a widget's own code, for the Error List.</summary>
public sealed record PluginWidgetErrorMessage(string PageId, string WidgetId, string Message);
