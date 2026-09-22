using System.Security.Cryptography;

namespace MacroStation.Core.Devices;

/// <summary>
/// Holds the one PIN new (unpaired) devices must enter to join. Generated at startup and whenever the
/// editor asks for a new one; a device that already has a token from a previous successful pairing never
/// needs it again. Deliberately in-memory only — restarting the server invalidates the current PIN,
/// which is fine since it's only meant to be read off the editor screen at pairing time, not persisted.
/// </summary>
public sealed class PairingService
{
    private readonly Lock _lock = new();
    private string _pin = GeneratePin();

    public string CurrentPin
    {
        get { lock (_lock) return _pin; }
    }

    public string Regenerate()
    {
        lock (_lock)
        {
            _pin = GeneratePin();
            return _pin;
        }
    }

    public bool Verify(string? pin) => !string.IsNullOrEmpty(pin) && pin == CurrentPin;

    private static string GeneratePin() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
}
