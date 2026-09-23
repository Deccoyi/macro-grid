using System.Text.Json;
using MacroStation.Core.Model;
using MacroStation.Protocol;

namespace MacroStation.Core.Devices;

/// <summary>Persists every paired device in one JSON file (unlike profiles, there's no reason to expect
/// enough devices to need one file each). Write-then-rename, same crash-safety as <c>ProfileStore</c>.</summary>
public sealed class DeviceStore
{
    private static readonly JsonSerializerOptions FileJson = new(ProtocolJson.Options) { WriteIndented = true };

    private readonly string _path;
    private readonly Lock _lock = new();
    private readonly Dictionary<string, PairedDevice> _devices = [];

    public DeviceStore(string dataDir)
    {
        Directory.CreateDirectory(dataDir);
        _path = Path.Combine(dataDir, "devices.json");
        Load();
    }

    public IReadOnlyList<PairedDevice> All
    {
        get { lock (_lock) return _devices.Values.OrderByDescending(d => d.LastSeenAt).ToList(); }
    }

    public PairedDevice? FindByToken(string? token)
    {
        if (string.IsNullOrEmpty(token)) return null;
        lock (_lock) return _devices.Values.FirstOrDefault(d => d.Token == token);
    }

    /// <summary>Issues a brand-new token for this device id, overwriting any previous pairing for it (a
    /// re-pair — e.g. the app was reinstalled and lost its cached token — just replaces the old record).</summary>
    public PairedDevice Pair(string deviceId, string deviceName)
    {
        var device = new PairedDevice
        {
            Id = deviceId,
            Name = deviceName,
            Token = Guid.NewGuid().ToString("N"),
            PairedAt = DateTimeOffset.UtcNow,
            LastSeenAt = DateTimeOffset.UtcNow,
        };
        lock (_lock) _devices[deviceId] = device;
        Save();
        return device;
    }

    public void Touch(string deviceId, string deviceName)
    {
        lock (_lock)
        {
            if (!_devices.TryGetValue(deviceId, out var device)) return;
            device.LastSeenAt = DateTimeOffset.UtcNow;
            device.Name = deviceName;
        }
        Save();
    }

    /// <summary>Sets or clears (null) the profile this device always opens. Returns false if the device isn't paired.</summary>
    public bool AssignProfile(string deviceId, string? profileId)
    {
        lock (_lock)
        {
            if (!_devices.TryGetValue(deviceId, out var device)) return false;
            device.AssignedProfileId = profileId;
        }
        Save();
        return true;
    }

    /// <summary>Sets whether this device's session auto-switches profile based on the server's foreground
    /// window. Returns false if the device isn't paired.</summary>
    public bool SetFollowActiveWindow(string deviceId, bool follow)
    {
        lock (_lock)
        {
            if (!_devices.TryGetValue(deviceId, out var device)) return false;
            device.FollowActiveWindow = follow;
        }
        Save();
        return true;
    }

    /// <summary>Sets the drawer's auto-switch lock, persisted so it survives a reconnect. Returns false if
    /// the device isn't paired.</summary>
    public bool SetAutoSwitchLocked(string deviceId, bool locked)
    {
        lock (_lock)
        {
            if (!_devices.TryGetValue(deviceId, out var device)) return false;
            device.AutoSwitchLocked = locked;
        }
        Save();
        return true;
    }

    public bool Revoke(string deviceId)
    {
        lock (_lock)
        {
            if (!_devices.Remove(deviceId)) return false;
        }
        Save();
        return true;
    }

    private void Load()
    {
        if (!File.Exists(_path)) return;
        try
        {
            var devices = JsonSerializer.Deserialize<List<PairedDevice>>(File.ReadAllText(_path), FileJson);
            if (devices is null) return;
            lock (_lock)
            {
                foreach (var d in devices) _devices[d.Id] = d;
            }
        }
        catch (JsonException)
        {
            File.Move(_path, _path + ".broken", overwrite: true);
        }
    }

    private void Save()
    {
        List<PairedDevice> snapshot;
        lock (_lock) snapshot = _devices.Values.ToList();
        var json = JsonSerializer.Serialize(snapshot, FileJson);
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, json);
        File.Move(tmp, _path, overwrite: true);
    }
}
