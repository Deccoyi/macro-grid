using System.Collections.Concurrent;
using MacroGrid.Core.Diagnostics;
using MacroGrid.Plugin.Abstractions;
using Microsoft.Extensions.Logging;

namespace MacroGrid.Core.Plugins.Images;

/// <summary>
/// The host side of button pictures that plugins supply. A profile holds only <c>plugin-image:&lt;pluginId&gt;/&lt;imageId&gt;</c>. The service keeps each plugin's
/// disk cache (<see cref="PluginImageCache"/>), answers "do we have it" for the layout and widget-state code, asks the plugin for what is missing
/// (batched and rate limited) and validates what the plugin sends back. A cached picture is served whether or not its plugin is running.
/// </summary>
public sealed class PluginImageService(ILogger<PluginImageService>? logger = null, ProblemList? problems = null, TimeProvider? time = null) : IDisposable
{
    /// <summary>How long a request stays open for the plugin's answer (and how long after it a repeated answer is still accepted).</summary>
    public static readonly TimeSpan RequestWindow = TimeSpan.FromMinutes(10);
    public const int MaxIdsPerCall = 20;
    public const int MaxOutstanding = 200;

    /// <summary>The one line a refused picture puts on the Error List: it never says what was offered.</summary>
    internal const string RefusedMessage = "A picture offered by this plugin was refused. It must be a PNG, JPEG, WebP or GIF of at most 64 KB that was asked for.";

    private sealed class Request(DateTimeOffset at)
    {
        public DateTimeOffset At { get; } = at;
        public bool Fulfilled { get; set; }
    }

    private sealed class Channel(string name, string dir, PluginImageCache cache)
    {
        public readonly Lock Lock = new();
        public string Name = name;
        public string Dir = dir;
        public PluginImageCache Cache = cache;
        public IPluginImageSource? Source;
        public readonly Dictionary<string, Request> Requested = new(StringComparer.Ordinal);
        public readonly List<string> Queue = [];
        public ITimer? Timer;
        public bool TimerArmed;
        public DateTimeOffset LastCall = DateTimeOffset.MinValue;
    }

    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private readonly ConcurrentDictionary<string, Channel> _channels = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _attachLock = new();
    private bool _disposed;

    /// <summary>The shortest time between two <see cref="IPluginImageSource.RequestImages"/> calls to one plugin.</summary>
    public TimeSpan MinCallInterval { get; init; } = TimeSpan.FromMilliseconds(500);

    /// <summary>Pictures of a plugin are new in the cache (the ids), or the plugin's folder became known (null): whatever shows them should look again.</summary>
    public event Action<string, IReadOnlyCollection<string>?>? ImagesChanged;

    public bool IsKnown(string pluginId) => _channels.ContainsKey(pluginId);

    /// <summary>Makes a plugin folder known, so its cached pictures can be served, and sets who answers requests (null while the plugin is not running).</summary>
    public void Attach(string pluginId, string pluginName, string pluginDir, IPluginImageSource? source)
    {
        bool isNew;
        Channel channel;
        lock (_attachLock)
        {
            if (_disposed) return;
            isNew = !_channels.TryGetValue(pluginId, out var existing) || !string.Equals(existing.Dir, pluginDir, StringComparison.OrdinalIgnoreCase);
            if (isNew) _channels[pluginId] = new Channel(pluginName, pluginDir, new PluginImageCache(pluginDir, _time));
            channel = _channels[pluginId];
        }
        lock (channel.Lock)
        {
            channel.Name = pluginName;
            channel.Source = source;
            Schedule(channel);
        }
        if (isNew) Raise(pluginId, null);
    }

    /// <summary>The plugin stopped: requests wait until it runs again, cached pictures keep being served.</summary>
    public void Detach(string pluginId)
    {
        if (_channels.TryGetValue(pluginId, out var channel))
            lock (channel.Lock) channel.Source = null;
    }

    /// <summary>The plugin was uninstalled: its cache and its open requests are gone.</summary>
    public void Forget(string pluginId)
    {
        if (!_channels.TryRemove(pluginId, out var channel)) return;
        lock (channel.Lock)
        {
            channel.Source = null;
            channel.Queue.Clear();
            channel.Requested.Clear();
            channel.Timer?.Dispose();
        }
        channel.Cache.Clear();
    }

    /// <summary>The picture as a data URL when it is cached; otherwise null, with nothing requested.</summary>
    public string? TryGetDataUrl(string pluginId, string imageId) =>
        _channels.TryGetValue(pluginId, out var channel) && channel.Cache.TryGet(imageId) is { } bytes ? PluginImageValidator.ToDataUrl(bytes) : null;

    /// <summary>The picture's bytes and media type when cached; otherwise null, and the plugin is asked for it.</summary>
    public (byte[] Bytes, string MediaType)? TryGetBytes(string pluginId, string imageId)
    {
        if (!_channels.TryGetValue(pluginId, out var channel)) return null;
        if (channel.Cache.TryGet(imageId) is { } bytes && PluginImageValidator.Sniff(bytes) is { } type) return (bytes, "image/" + type);
        RequestImage(pluginId, imageId);
        return null;
    }

    /// <summary>For a reference in a profile: the data URL when cached, else null and the plugin is asked for the picture.</summary>
    public string? Resolve(string reference)
    {
        if (!PluginImageRef.TryParse(reference, out var pluginId, out var imageId)) return null;
        var url = TryGetDataUrl(pluginId, imageId);
        if (url is null) RequestImage(pluginId, imageId);
        return url;
    }

    /// <summary>Asks the plugin for a picture unless it is already asked for (and the question is younger than <see cref="RequestWindow"/>) or 200 are open.</summary>
    public void RequestImage(string pluginId, string imageId)
    {
        if (!_channels.TryGetValue(pluginId, out var channel) || !PluginImageRef.IsValidImageId(imageId)) return;
        lock (channel.Lock)
        {
            var now = _time.GetUtcNow();
            foreach (var stale in channel.Requested.Where(r => now - r.Value.At >= RequestWindow).Select(r => r.Key).ToList())
                channel.Requested.Remove(stale);
            if (channel.Requested.TryGetValue(imageId, out var open) && !open.Fulfilled) return;
            if (channel.Requested.Values.Count(r => !r.Fulfilled) >= MaxOutstanding) return;
            channel.Requested[imageId] = new Request(now);
            if (!channel.Queue.Contains(imageId)) channel.Queue.Add(imageId);
            Schedule(channel);
        }
    }

    /// <summary>A plugin hands over a picture. Accepted only when it is valid and was asked for within <see cref="RequestWindow"/>; a refusal is one problem line per plugin.</summary>
    public bool Put(string pluginId, string imageId, string dataUrl)
    {
        if (!_channels.TryGetValue(pluginId, out var channel)) return false;
        var bytes = Array.Empty<byte>();
        var valid = PluginImageRef.IsValidImageId(imageId)
            && PluginImageValidator.TryValidate(dataUrl, out bytes, out _) == PluginImageValidator.Failure.None;
        if (valid)
            lock (channel.Lock)
                valid = channel.Requested.TryGetValue(imageId, out var asked) && _time.GetUtcNow() - asked.At < RequestWindow;
        if (!valid)
        {
            problems?.Report(pluginId, channel.Name, ProblemSeverity.Warning, ProblemCodes.ImageRefused, RefusedMessage);
            logger?.LogWarning("Plugin {PluginId} offered a picture the host refused", pluginId);
            return false;
        }

        if (!channel.Cache.Put(imageId, bytes))
        {
            logger?.LogWarning("A picture of plugin {PluginId} could not be written to the cache", pluginId);
            return false;
        }
        lock (channel.Lock)
            if (channel.Requested.TryGetValue(imageId, out var fulfilled)) fulfilled.Fulfilled = true;
        Raise(pluginId, [imageId]);
        return true;
    }

    // Called with the channel lock held.
    private void Schedule(Channel channel)
    {
        if (channel.Source is null || channel.Queue.Count == 0 || channel.TimerArmed || _disposed) return;
        var wait = channel.LastCall + MinCallInterval - _time.GetUtcNow();
        Arm(channel, wait < TimeSpan.Zero ? TimeSpan.Zero : wait);
    }

    private void Arm(Channel channel, TimeSpan delay)
    {
        channel.TimerArmed = true;
        channel.Timer ??= _time.CreateTimer(_ => Flush(channel), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        channel.Timer.Change(delay, Timeout.InfiniteTimeSpan);
    }

    private void Flush(Channel channel)
    {
        string[] batch;
        IPluginImageSource source;
        lock (channel.Lock)
        {
            channel.TimerArmed = false;
            if (channel.Source is not { } current || channel.Queue.Count == 0) return;
            source = current;
            var now = _time.GetUtcNow();
            var take = new List<string>();
            var examined = 0;
            foreach (var id in channel.Queue)
            {
                if (take.Count == MaxIdsPerCall) break;
                examined++;
                if (channel.Requested.TryGetValue(id, out var r) && !r.Fulfilled && now - r.At < RequestWindow) take.Add(id);
            }
            channel.Queue.RemoveRange(0, examined);
            if (take.Count == 0) return;
            batch = [.. take];
            channel.LastCall = now;
            Schedule(channel);
        }

        try { source.RequestImages(batch); }
        catch (Exception ex)
        {
            logger?.LogWarning("Plugin {PluginId} failed while asked for pictures ({Error})", PluginIdOf(channel), ex.GetType().Name);
        }
    }

    private string PluginIdOf(Channel channel) => _channels.FirstOrDefault(kv => ReferenceEquals(kv.Value, channel)).Key ?? "?";

    private void Raise(string pluginId, IReadOnlyCollection<string>? ids)
    {
        try { ImagesChanged?.Invoke(pluginId, ids); }
        catch (Exception ex) { logger?.LogWarning("A handler of new plugin pictures failed ({Error})", ex.GetType().Name); }
    }

    public void Dispose()
    {
        lock (_attachLock) _disposed = true;
        foreach (var channel in _channels.Values) lock (channel.Lock) channel.Timer?.Dispose();
    }
}
