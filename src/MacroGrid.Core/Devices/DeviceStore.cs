using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MacroGrid.Core.Model;
using MacroGrid.Core.Security;
using MacroGrid.Protocol;

namespace MacroGrid.Core.Devices;

/// <summary>Persists every paired device in one JSON file (unlike profiles, there's no reason to expect
/// enough devices to need one file each). Write-then-rename, same crash-safety as <c>ProfileStore</c>.
/// With an <see cref="ISecretProtector"/> each token is written encrypted as <c>protectedToken</c> instead of
/// <c>token</c>; a file from before that is converted on load. A token that cannot be decrypted here (the file came
/// from another Windows user or PC) drops that device, which then has to pair again.</summary>
public sealed class DeviceStore
{
    private static readonly JsonSerializerOptions FileJson = new(ProtocolJson.Options) { WriteIndented = true };
    private static readonly string TokenKey = FileJson.PropertyNamingPolicy?.ConvertName(nameof(PairedDevice.Token)) ?? nameof(PairedDevice.Token);
    private const string ProtectedTokenKey = "protectedToken";

    private readonly string _path;
    private readonly ISecretProtector? _protector;
    private readonly Lock _lock = new();
    private readonly Dictionary<string, PairedDevice> _devices = [];

    public DeviceStore(string dataDir, ISecretProtector? protector = null)
    {
        Directory.CreateDirectory(dataDir);
        _path = Path.Combine(dataDir, "devices.json");
        _protector = protector;
        Load();
    }

    /// <summary>Devices dropped at load because their token could not be decrypted here.</summary>
    public int UnreadableOnLoad { get; private set; }

    public IReadOnlyList<PairedDevice> All
    {
        get { lock (_lock) return _devices.Values.OrderByDescending(d => d.LastSeenAt).ToList(); }
    }

    public PairedDevice? FindByToken(string? token)
    {
        if (string.IsNullOrEmpty(token)) return null;
        var sent = Encoding.UTF8.GetBytes(token);
        // Fixed-time comparison, so the time an answer takes says nothing about how much of a token was right.
        lock (_lock) return _devices.Values.FirstOrDefault(d => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(d.Token), sent));
    }

    /// <summary>Issues a brand-new token for this device id, overwriting any previous pairing for it (a
    /// re-pair — e.g. the app was reinstalled and lost its cached token — just replaces the old record).</summary>
    public PairedDevice Pair(string deviceId, string deviceName)
    {
        var device = new PairedDevice
        {
            Id = deviceId,
            Name = deviceName,
            Token = RandomNumberGenerator.GetHexString(32, lowercase: true),
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
        var convert = false;
        try
        {
            if (JsonNode.Parse(File.ReadAllText(_path)) is not JsonArray array) return;
            var readable = new JsonArray();
            foreach (var node in array.ToList())
            {
                if (node is not JsonObject device) continue;
                array.Remove(device);
                if (device[ProtectedTokenKey] is JsonValue protectedValue && protectedValue.TryGetValue<string>(out var protectedToken))
                {
                    var token = _protector?.Unprotect(protectedToken);
                    if (token is null)
                    {
                        UnreadableOnLoad++;
                        convert = true;
                        continue;
                    }
                    device.Remove(ProtectedTokenKey);
                    device[TokenKey] = token;
                }
                else if (_protector is not null)
                {
                    convert = true; // A plain token from an older version: write it encrypted.
                }
                readable.Add(device);
            }

            var devices = readable.Deserialize<List<PairedDevice>>(FileJson);
            if (devices is null) return;
            lock (_lock)
            {
                foreach (var d in devices) _devices[d.Id] = d;
            }
        }
        catch (JsonException)
        {
            File.Move(_path, _path + ".broken", overwrite: true);
            return;
        }
        if (convert) Save();
    }

    private void Save()
    {
        List<PairedDevice> snapshot;
        lock (_lock) snapshot = _devices.Values.ToList();
        var array = JsonSerializer.SerializeToNode(snapshot, FileJson)!.AsArray();
        if (_protector is not null)
        {
            foreach (var node in array)
            {
                if (node is not JsonObject device || device[TokenKey]?.GetValue<string>() is not { } token) continue;
                device.Remove(TokenKey);
                device[ProtectedTokenKey] = _protector.Protect(token);
            }
        }
        var json = array.ToJsonString(FileJson);
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, json);
        File.Move(tmp, _path, overwrite: true);
    }
}
