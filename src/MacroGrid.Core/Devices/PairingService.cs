using System.Security.Cryptography;
using System.Text;

namespace MacroGrid.Core.Devices;

/// <summary>The PIN currently shown in the editor's Pairing window and when it expires.</summary>
public readonly record struct PairingCode(string Pin, DateTimeOffset ExpiresAt);

public enum PairingOutcome
{
    /// <summary>The PIN was right; the caller pairs the device. The PIN is used up and replaced.</summary>
    Accepted,

    /// <summary>No PIN was sent (an unpaired device asking what to do). Not counted as a failed attempt.</summary>
    PinRequired,

    /// <summary>A PIN was sent but nobody has the Pairing window open, so no PIN is valid at all.</summary>
    Closed,

    /// <summary>A PIN was sent and it was wrong.</summary>
    WrongPin,

    /// <summary>This address sent too many wrong PINs and has to wait (<see cref="PairingResult.RetryAfter"/>).</summary>
    Blocked,
}

/// <param name="Failures">Wrong PINs from this address in a row (for the log).</param>
/// <param name="PinRenewed">The PIN was replaced because too many wrong PINs were tried against it.</param>
/// <param name="NewlyBlocked">This attempt started the block (later attempts during the block are not logged again).</param>
public readonly record struct PairingResult(PairingOutcome Outcome, TimeSpan RetryAfter = default, int Failures = 0, bool PinRenewed = false, bool NewlyBlocked = false);

/// <summary>
/// Pairing works like a pairing mode: a PIN is valid only while the editor's Pairing window is open (the window
/// renews a lease by polling, see <see cref="Keep"/>) and at most <see cref="Ttl"/> long. With the window closed no
/// PIN is valid, so nobody on the network can guess one. A PIN is used up by a successful pairing.
///
/// Guessing is also limited: after <see cref="FailuresBeforeBlock"/> wrong PINs an address is blocked for a while
/// (doubling each time, up to <see cref="MaxBlock"/>), and after <see cref="FailuresBeforeNewPin"/> wrong PINs in
/// total, from any address, the PIN itself is replaced. Everything is in memory; a restart closes pairing.
/// </summary>
public sealed class PairingService
{
    public static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    /// <summary>How long pairing stays open after the Pairing window last asked for the PIN.</summary>
    public static readonly TimeSpan Lease = TimeSpan.FromSeconds(15);

    internal const int FailuresBeforeBlock = 5;
    internal const int FailuresBeforeNewPin = 20;
    internal static readonly TimeSpan FirstBlock = TimeSpan.FromSeconds(30);
    internal static readonly TimeSpan MaxBlock = TimeSpan.FromMinutes(15);

    /// <summary>Bounds the per-address table so a flood of spoofed addresses cannot grow memory.</summary>
    internal const int MaxTrackedAddresses = 256;

    private sealed class AddressState
    {
        public int Failures;
        public DateTimeOffset LastFailure;
        public DateTimeOffset BlockedUntil;
    }

    private readonly TimeProvider _time;
    private readonly Lock _lock = new();
    private readonly Dictionary<string, AddressState> _addresses = new(StringComparer.Ordinal);
    private string _pin;
    private DateTimeOffset _issuedAt;
    private DateTimeOffset _leaseUntil = DateTimeOffset.MinValue;
    private int _failuresOnPin;

    public PairingService() : this(TimeProvider.System) { }

    internal PairingService(TimeProvider time)
    {
        _time = time;
        _pin = GeneratePin();
        _issuedAt = time.GetUtcNow();
    }

    public bool IsOpen
    {
        get { lock (_lock) return IsOpenLocked(_time.GetUtcNow()); }
    }

    internal int TrackedAddressCount
    {
        get { lock (_lock) return _addresses.Count; }
    }

    /// <summary>The Pairing window opened or asked for a new code: opens pairing with a fresh PIN.</summary>
    public PairingCode Open()
    {
        lock (_lock)
        {
            var now = _time.GetUtcNow();
            RenewPinLocked(now);
            _leaseUntil = now + Lease;
            return CodeLocked();
        }
    }

    /// <summary>The open Pairing window polls this: keeps pairing open and returns the current PIN, replacing it
    /// when it expired (or when pairing had closed in between, for example while the PC was asleep).</summary>
    public PairingCode Keep()
    {
        lock (_lock)
        {
            var now = _time.GetUtcNow();
            if (!IsOpenLocked(now)) RenewPinLocked(now);
            _leaseUntil = now + Lease;
            return CodeLocked();
        }
    }

    /// <summary>Checks a PIN sent by a device at <paramref name="remoteAddress"/>.</summary>
    public PairingResult TryPair(string? pin, string remoteAddress)
    {
        lock (_lock)
        {
            var now = _time.GetUtcNow();
            if (string.IsNullOrEmpty(pin)) return new PairingResult(PairingOutcome.PinRequired);

            var address = GetAddressLocked(remoteAddress, now);
            if (address.BlockedUntil > now)
                return new PairingResult(PairingOutcome.Blocked, address.BlockedUntil - now, address.Failures);

            var open = IsOpenLocked(now);
            if (open && FixedTimeEquals(pin, _pin))
            {
                _addresses.Remove(remoteAddress);
                RenewPinLocked(now);
                return new PairingResult(PairingOutcome.Accepted);
            }

            address.Failures++;
            address.LastFailure = now;
            var retryAfter = TimeSpan.Zero;
            if (address.Failures % FailuresBeforeBlock == 0)
            {
                retryAfter = BlockDuration(address.Failures / FailuresBeforeBlock);
                address.BlockedUntil = now + retryAfter;
            }

            var renewed = false;
            if (open && ++_failuresOnPin >= FailuresBeforeNewPin)
            {
                RenewPinLocked(now);
                renewed = true;
            }

            var outcome = retryAfter > TimeSpan.Zero ? PairingOutcome.Blocked : open ? PairingOutcome.WrongPin : PairingOutcome.Closed;
            return new PairingResult(outcome, retryAfter, address.Failures, renewed, NewlyBlocked: retryAfter > TimeSpan.Zero);
        }
    }

    /// <summary>30 s, 1 min, 2 min, ... up to <see cref="MaxBlock"/>.</summary>
    internal static TimeSpan BlockDuration(int round)
    {
        var seconds = FirstBlock.TotalSeconds * Math.Pow(2, Math.Min(round - 1, 10));
        return TimeSpan.FromSeconds(Math.Min(seconds, MaxBlock.TotalSeconds));
    }

    private bool IsOpenLocked(DateTimeOffset now) => now < _leaseUntil && now - _issuedAt < Ttl;

    private PairingCode CodeLocked() => new(_pin, _issuedAt + Ttl);

    private void RenewPinLocked(DateTimeOffset now)
    {
        _pin = GeneratePin();
        _issuedAt = now;
        _failuresOnPin = 0;
    }

    private AddressState GetAddressLocked(string remoteAddress, DateTimeOffset now)
    {
        if (_addresses.TryGetValue(remoteAddress, out var state))
        {
            // A quiet address starts over, so an old typo does not count against a new attempt.
            if (state.BlockedUntil <= now && now - state.LastFailure > MaxBlock) state.Failures = 0;
            return state;
        }

        if (_addresses.Count >= MaxTrackedAddresses) PruneLocked(now);
        state = new AddressState();
        _addresses[remoteAddress] = state;
        return state;
    }

    private void PruneLocked(DateTimeOffset now)
    {
        foreach (var (key, state) in _addresses.ToList())
        {
            if (state.BlockedUntil <= now && now - state.LastFailure > MaxBlock) _addresses.Remove(key);
        }
        // Still full: drop the addresses that failed longest ago. Their blocks end early, but the per-PIN limit
        // still holds, so a flood of addresses cannot guess the PIN either.
        if (_addresses.Count >= MaxTrackedAddresses)
        {
            foreach (var key in _addresses.OrderBy(a => a.Value.LastFailure).Take(_addresses.Count - MaxTrackedAddresses + 1).Select(a => a.Key).ToList())
                _addresses.Remove(key);
        }
    }

    private static bool FixedTimeEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));

    private static string GeneratePin() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
}
