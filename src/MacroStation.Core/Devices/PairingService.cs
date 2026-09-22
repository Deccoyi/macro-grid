using System.Security.Cryptography;

namespace MacroStation.Core.Devices;

/// <summary>
/// Holds the one PIN new (unpaired) devices must enter to join. Generated at startup, whenever the
/// editor asks for a new one, and auto-rotated after <see cref="Ttl"/> elapses (checked lazily on
/// access — there's no background timer). A device that already has a token from a previous successful
/// pairing never needs it again. Deliberately in-memory only — restarting the server invalidates the
/// current PIN, which is fine since it's only meant to be read off the editor screen (or scanned from
/// its QR code) at pairing time, not persisted.
/// </summary>
public sealed class PairingService
{
    public static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    private readonly Lock _lock = new();
    private string _pin = GeneratePin();
    private DateTimeOffset _issuedAt = DateTimeOffset.UtcNow;

    public string CurrentPin
    {
        get
        {
            lock (_lock)
            {
                if (DateTimeOffset.UtcNow - _issuedAt >= Ttl) RegenerateLocked();
                return _pin;
            }
        }
    }

    public DateTimeOffset ExpiresAt
    {
        get { lock (_lock) return _issuedAt + Ttl; }
    }

    public string Regenerate()
    {
        lock (_lock)
        {
            RegenerateLocked();
            return _pin;
        }
    }

    public bool Verify(string? pin) => !string.IsNullOrEmpty(pin) && pin == CurrentPin;

    private void RegenerateLocked()
    {
        _pin = GeneratePin();
        _issuedAt = DateTimeOffset.UtcNow;
    }

    private static string GeneratePin() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
}
