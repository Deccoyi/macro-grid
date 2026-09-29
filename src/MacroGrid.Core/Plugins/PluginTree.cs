using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Core.Plugins;

/// <summary>Asks a plugin's <see cref="IPluginTreeProvider"/> for one level of its tree, with the limits the
/// editor relies on: a 2 second timeout, at most <see cref="MaxItemsPerPage"/> items, and no item without an id or
/// with an id already seen on the page (a plugin bug must not break the editor's tree).</summary>
public static class PluginTreeReader
{
    public const int MaxItemsPerPage = 500;
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(2);

    /// <exception cref="TimeoutException">The provider did not answer within <paramref name="timeout"/>.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled (the editor gave up).</exception>
    public static async Task<PluginTreePage> ReadAsync(
        IPluginTreeProvider provider, string? parentId, string? continuationToken, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(timeout);

        PluginTreePage page;
        try
        {
            // WaitAsync as well as the token: a provider that ignores its token still cannot hold the request.
            page = await provider.GetTreeItemsAsync(NullIfEmpty(parentId), NullIfEmpty(continuationToken), linked.Token)
                .WaitAsync(linked.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"The plugin did not list its items within {timeout.TotalSeconds:0.#} s.");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var items = new List<PluginTreeItem>();
        foreach (var item in page?.Items ?? [])
        {
            if (item is null || string.IsNullOrEmpty(item.Id) || !seen.Add(item.Id)) continue;
            items.Add(item with { Label = item.Label ?? item.Id });
            if (items.Count == MaxItemsPerPage) break;
        }
        return new PluginTreePage(items, NullIfEmpty(page?.ContinuationToken));
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;
}

/// <summary>One <see cref="IPluginTreeProvider.TreeItemsChanged"/> notice: which level of which plugin changed
/// (<see cref="ParentId"/> null: the plugin's top level; <see cref="PluginId"/> alone with
/// <see cref="WholePlugin"/>: the plugin was loaded, reloaded or removed, so all of it is stale).</summary>
public sealed record PluginTreeChange(long Revision, string PluginId, string? ParentId, bool WholePlugin = false);

/// <summary>The answer to "what changed since revision N": the current revision, the changes after N, and
/// <see cref="Reset"/> when N is older than what is still kept (the editor then drops its whole cache).</summary>
public sealed record PluginTreeChanges(long Revision, IReadOnlyList<PluginTreeChange> Changes, bool Reset);

/// <summary>A small, bounded log of tree changes the editor polls while its Plugins tool window is on screen, so a
/// plugin's <see cref="IPluginTreeProvider.TreeItemsChanged"/> reaches the editor without a push channel. Memory is
/// fixed (the last <see cref="Capacity"/> entries); nothing is written unless a plugin raises the event.</summary>
public sealed class PluginTreeChangeLog
{
    public const int Capacity = 256;

    private readonly Lock _lock = new();
    private readonly Queue<PluginTreeChange> _changes = new();
    private long _revision;

    public long Revision
    {
        get { lock (_lock) return _revision; }
    }

    public void Record(string pluginId, string? parentId, bool wholePlugin = false)
    {
        lock (_lock)
        {
            _revision++;
            _changes.Enqueue(new PluginTreeChange(_revision, pluginId, string.IsNullOrEmpty(parentId) ? null : parentId, wholePlugin));
            while (_changes.Count > Capacity) _changes.Dequeue();
        }
    }

    public PluginTreeChanges Since(long revision)
    {
        lock (_lock)
        {
            // A negative "since" is a fresh editor asking for the current revision only.
            if (revision < 0 || revision == _revision) return new PluginTreeChanges(_revision, [], false);
            // Newer than this log has ever been: the server restarted under a running editor.
            if (revision > _revision) return new PluginTreeChanges(_revision, [], true);
            var oldestKept = _changes.Count == 0 ? _revision + 1 : _changes.Peek().Revision;
            if (revision + 1 < oldestKept) return new PluginTreeChanges(_revision, [], true);
            return new PluginTreeChanges(_revision, [.. _changes.Where(c => c.Revision > revision)], false);
        }
    }
}
