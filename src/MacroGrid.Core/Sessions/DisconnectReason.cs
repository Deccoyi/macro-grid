using System.Net.WebSockets;

namespace MacroGrid.Core.Sessions;

/// <summary>Turns the way a client session ended into one short log text. Never includes message contents, only the close status/description or the exception type and message.</summary>
public static class DisconnectReason
{
    private const int MaxDescriptionChars = 100;

    public static string Describe(WebSocketCloseStatus? status, string? description) =>
        status is null
            ? "client closed without a status"
            : $"client closed: {(int)status.Value} {status}" + (string.IsNullOrEmpty(description) ? "" : $" \"{Clean(description)}\"");

    public static string Describe(Exception ex) =>
        $"{ex.GetType().Name}: {Clean(ex.Message)}" + (ex is WebSocketException { WebSocketErrorCode: not WebSocketError.Success } w ? $" ({w.WebSocketErrorCode})" : "");

    private static string Clean(string text)
    {
        var flat = new string(text.Select(c => char.IsControl(c) ? ' ' : c).ToArray());
        return flat.Length <= MaxDescriptionChars ? flat : flat[..MaxDescriptionChars] + "…";
    }
}
