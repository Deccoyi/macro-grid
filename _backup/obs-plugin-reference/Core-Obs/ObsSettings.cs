namespace MacroStation.Core.Obs;

/// <summary>
/// OBS WebSocket (obs-websocket v5, built into OBS 28+) connection settings, stored in
/// <see cref="Preferences.AppPreferences"/> alongside the rest of the user's server-side prefs — not
/// per-profile, one OBS instance per server the way the plan describes it. The password is stored in
/// plain text in the same preferences.json as everything else (no OS keychain integration yet); this is
/// consistent with pairing device tokens also living on disk in plain JSON, but worth revisiting if
/// plugin settings ever need real secrecy.
/// </summary>
public sealed class ObsSettings
{
    public bool Enabled { get; set; }
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 4455;
    public string Password { get; set; } = "";
}
