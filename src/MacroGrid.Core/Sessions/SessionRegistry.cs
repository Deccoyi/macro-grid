using System.Collections.Concurrent;
using MacroGrid.Core.Widgets;

namespace MacroGrid.Core.Sessions;

/// <summary>
/// Tracks every connected client. <see cref="Core.Sessions.ClientHub"/> owns add/remove (connection lifecycle);
/// <see cref="WidgetStateService"/> only reads <see cref="All"/> to know who to push updates to.
/// Splitting this out of ClientHub avoids a circular dependency between the two.
/// </summary>
public sealed class SessionRegistry
{
    private readonly ConcurrentDictionary<string, ClientSession> _sessions = new();

    /// <summary>Fires when a client connects, disconnects, or otherwise changes (e.g. becomes identified after hello).</summary>
    public event Action? Changed;

    public IReadOnlyCollection<ClientSession> All => _sessions.Values.ToList();

    internal void Add(ClientSession session)
    {
        _sessions[session.Id] = session;
        Changed?.Invoke();
    }

    internal void Remove(ClientSession session)
    {
        _sessions.TryRemove(session.Id, out _);
        Changed?.Invoke();
    }

    internal void NotifyChanged() => Changed?.Invoke();
}
