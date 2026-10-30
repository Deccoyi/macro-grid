using MacroGrid.Core.Diagnostics;

namespace MacroGrid.Core.Plugins;

/// <summary>What happened to a plugin's notice.</summary>
public enum NoticeResult { Shown, Dropped, NoDisplay }

/// <summary>
/// Short notices a JavaScript plugin with the <c>notify</c> permission can ask the shell to show (a tray balloon). The title is always the
/// plugin's own name and the text is cleaned and cut here, so a plugin cannot pose as Macro Grid or hide text. Each plugin gets a few notices
/// in a row, then one every 30 seconds; the rest are dropped.
/// </summary>
public sealed class PluginNotifications(Func<DateTimeOffset>? clock = null)
{
    public const int MaxTextLength = 200;
    public const int MaxTitleLength = 60;
    private const int Burst = 3;
    private static readonly TimeSpan Refill = TimeSpan.FromSeconds(30);

    private readonly Func<DateTimeOffset> _clock = clock ?? (() => DateTimeOffset.UtcNow);
    private readonly Dictionary<string, (double Tokens, DateTimeOffset At)> _buckets = [];
    private readonly Lock _lock = new();

    /// <summary>Raised with (title, text) for the shell to show. Only the tray of the running app listens.</summary>
    public event Action<string, string>? Posted;

    public NoticeResult Post(string pluginId, string pluginName, string text)
    {
        var handler = Posted;
        if (handler is null) return NoticeResult.NoDisplay;

        var cleaned = PlainText.Clean(text, MaxTextLength).Trim();
        if (cleaned.Length == 0) return NoticeResult.Dropped;
        if (!TakeToken(pluginId)) return NoticeResult.Dropped;

        var title = PlainText.Clean(pluginName, MaxTitleLength).Trim();
        handler(title.Length == 0 ? pluginId : title, cleaned);
        return NoticeResult.Shown;
    }

    private bool TakeToken(string pluginId)
    {
        lock (_lock)
        {
            var now = _clock();
            var (tokens, at) = _buckets.TryGetValue(pluginId, out var b) ? b : (Burst, now);
            tokens = Math.Min(Burst, tokens + (now - at) / Refill);
            if (tokens < 1)
            {
                _buckets[pluginId] = (tokens, now);
                return false;
            }
            _buckets[pluginId] = (tokens - 1, now);
            return true;
        }
    }
}
