using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using MacroGrid.Core.Languages;
using MacroGrid.Core.Model;
using MacroGrid.Core.Preferences;
using MacroGrid.Core.Profiles;
using MacroGrid.Core.Variables;
using MacroGrid.Protocol;

namespace MacroGrid.Core.Backup;

/// <summary>What a device keeps in a backup: the choices made in the editor, never the token or pairing times.</summary>
public sealed record BackupDevice(string Id, string Name, string? AssignedProfileId, bool FollowActiveWindow, bool AutoSwitchLocked);

/// <param name="Kind"><c>backup</c> (made on request) or <c>restorePoint</c> (made automatically before something is overwritten).</param>
/// <param name="ContentHash">SHA-256 over the other entries; only used to skip a restore point that would hold the same data as the newest one.</param>
public sealed record BackupManifest(
    int FormatVersion,
    string Kind,
    string Reason,
    DateTimeOffset CreatedAt,
    string ServerVersion,
    string ContentHash,
    IReadOnlyList<PackagePluginRef> RequiredPlugins);

/// <summary>Everything a backup holds, as plain data. Secrets have no place in it: a device has no token field, and plugin settings arrive with their password fields already removed.</summary>
public sealed class BackupContent
{
    public List<Profile> Profiles { get; init; } = [];
    public List<ProfileTreeNode> ProfileTree { get; init; } = [];
    public AppPreferences? Preferences { get; init; }
    public List<UserVariable> Variables { get; init; } = [];
    public List<BackupDevice> Devices { get; init; } = [];
    /// <summary>Plugin id to the settings-page values of that plugin.</summary>
    public Dictionary<string, JsonObject> PluginSettings { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Language tag to the pack file text.</summary>
    public Dictionary<string, string> LanguagePacks { get; init; } = new(StringComparer.Ordinal);
}

public sealed record BackupFileContent(BackupManifest Manifest, BackupContent Content);

/// <summary>
/// The <c>.mgbackup</c> file: a zip of <c>manifest.json</c> and one entry per kind of data. Reading is defensive, like
/// <see cref="ProfilePackage"/>: only the entry names below are read (an id or tag in a name must pass the same checks the stores use),
/// every read is bounded, a newer format is refused, and every profile goes through the same checks as a shared one.
/// </summary>
public static partial class BackupFile
{
    public const int CurrentFormatVersion = 1;
    public const string Extension = ".mgbackup";
    public const string KindBackup = "backup";
    public const string KindRestorePoint = "restorePoint";

    internal const long MaxEntryBytes = 64 * 1024 * 1024;
    internal const long MaxTotalBytes = 256L * 1024 * 1024;
    internal const int MaxEntries = 2000;

    private const string ManifestEntry = "manifest.json";
    private const string TreeEntry = "profile-tree.json";
    private const string PreferencesEntry = "preferences.json";
    private const string VariablesEntry = "user-variables.json";
    private const string DevicesEntry = "devices.json";

    private static readonly JsonSerializerOptions Json = new(ProtocolJson.Options) { WriteIndented = true };

    [GeneratedRegex("^[A-Za-z0-9_-]{1,64}$")]
    private static partial Regex IdPattern();

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$")]
    private static partial Regex PluginIdPattern();

    public static bool IsValidProfileId(string? id) => id is not null && IdPattern().IsMatch(id);

    /// <summary>Builds the file. The manifest's <paramref name="requiredPlugins"/> are the plugins the profiles' actions need.</summary>
    public static byte[] Write(BackupContent content, string kind, string reason, string serverVersion, IReadOnlyList<PackagePluginRef> requiredPlugins, DateTimeOffset? now = null)
    {
        var entries = Serialize(content);
        var manifest = new BackupManifest(CurrentFormatVersion, kind, reason, now ?? DateTimeOffset.UtcNow, serverVersion, Hash(entries), requiredPlugins);

        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(zip, ManifestEntry, JsonSerializer.SerializeToUtf8Bytes(manifest, Json));
            foreach (var (name, bytes) in entries) WriteEntry(zip, name, bytes);
        }
        return buffer.ToArray();
    }

    /// <summary>The content hash a file written from <paramref name="content"/> would carry.</summary>
    public static string HashOf(BackupContent content) => Hash(Serialize(content));

    /// <exception cref="InvalidDataException">The file is not a usable backup; the message says why.</exception>
    public static BackupFileContent Read(byte[] bytes)
    {
        if (bytes.Length < 4 || bytes[0] != 'P' || bytes[1] != 'K') throw new InvalidDataException("This file is not a Macro Grid backup.");
        try
        {
            using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
            if (zip.Entries.Count > MaxEntries) throw new InvalidDataException("The backup has too many entries.");

            var manifestEntry = zip.GetEntry(ManifestEntry) ?? throw new InvalidDataException("This file is not a Macro Grid backup.");
            BackupManifest manifest;
            try { manifest = JsonSerializer.Deserialize<BackupManifest>(ReadEntry(manifestEntry), Json) ?? throw new InvalidDataException("The backup manifest is damaged."); }
            catch (JsonException ex) { throw new InvalidDataException("The backup manifest is damaged.", ex); }
            if (manifest.FormatVersion > CurrentFormatVersion)
                throw new InvalidDataException($"This backup was made by a newer version (backup format {manifest.FormatVersion}); update Macro Grid to open it.");

            long total = 0;
            var profiles = new List<Profile>();
            var languages = new Dictionary<string, string>(StringComparer.Ordinal);
            var pluginSettings = new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);
            List<ProfileTreeNode> tree = [];
            AppPreferences? preferences = null;
            List<UserVariable> variables = [];
            List<BackupDevice> devices = [];

            foreach (var entry in zip.Entries)
            {
                var name = entry.FullName;
                if (name == ManifestEntry) continue;
                var kind = Classify(name, out var key);
                if (kind == EntryKind.Unknown) continue;

                var data = ReadEntry(entry);
                total += data.Length;
                if (total > MaxTotalBytes) throw new InvalidDataException("The backup is too large.");

                switch (kind)
                {
                    case EntryKind.Profile:
                        var profile = ProfilePackage.ParseProfile(data);
                        if (!IsValidProfileId(profile.Id) || !profile.Id.Equals(key, StringComparison.Ordinal)) throw new InvalidDataException($"The profile in '{name}' does not match its name.");
                        profiles.Add(profile);
                        break;
                    case EntryKind.Tree:
                        tree = Parse<List<ProfileTreeNode>>(data, name) ?? [];
                        break;
                    case EntryKind.Preferences:
                        preferences = Parse<AppPreferences>(data, name);
                        break;
                    case EntryKind.Variables:
                        variables = Parse<List<UserVariable>>(data, name) ?? [];
                        break;
                    case EntryKind.Devices:
                        devices = (Parse<List<BackupDevice>>(data, name) ?? []).Where(d => IsValidProfileId(d.Id) && !string.IsNullOrWhiteSpace(d.Name)).ToList();
                        break;
                    case EntryKind.PluginSettings:
                        if (JsonNode.Parse(data) is JsonObject settings) pluginSettings[key] = settings;
                        break;
                    case EntryKind.Language:
                        var text = Encoding.UTF8.GetString(data);
                        if (LanguagePackStore.IsValidTag(key) && data.Length <= LanguagePackStore.MaxBytes) languages[key] = text;
                        break;
                }
            }

            var content = new BackupContent
            {
                Profiles = profiles, ProfileTree = tree, Preferences = preferences, Variables = variables,
                Devices = devices, PluginSettings = pluginSettings, LanguagePacks = languages,
            };
            return new BackupFileContent(manifest, content);
        }
        catch (InvalidDataException) { throw; }
        catch (Exception ex) when (ex is IOException or NotSupportedException or JsonException)
        {
            throw new InvalidDataException("The file could not be read as a backup.", ex);
        }
    }

    private enum EntryKind { Unknown, Profile, Tree, Preferences, Variables, Devices, PluginSettings, Language }

    private static EntryKind Classify(string name, out string key)
    {
        key = "";
        switch (name)
        {
            case TreeEntry: return EntryKind.Tree;
            case PreferencesEntry: return EntryKind.Preferences;
            case VariablesEntry: return EntryKind.Variables;
            case DevicesEntry: return EntryKind.Devices;
        }
        if (TryKey(name, "profiles/", out key) && IdPattern().IsMatch(key)) return EntryKind.Profile;
        if (TryKey(name, "plugin-settings/", out key) && PluginIdPattern().IsMatch(key) && !key.Contains("..")) return EntryKind.PluginSettings;
        if (TryKey(name, "languages/", out key) && LanguagePackStore.IsValidTag(key)) return EntryKind.Language;
        key = "";
        return EntryKind.Unknown;
    }

    private static bool TryKey(string name, string folder, out string key)
    {
        key = "";
        if (!name.StartsWith(folder, StringComparison.Ordinal) || !name.EndsWith(".json", StringComparison.Ordinal)) return false;
        key = name[folder.Length..^".json".Length];
        return key.Length > 0;
    }

    private static T? Parse<T>(byte[] data, string name)
    {
        try { return JsonSerializer.Deserialize<T>(data, Json); }
        catch (JsonException ex) { throw new InvalidDataException($"'{name}' in the backup is damaged.", ex); }
    }

    /// <summary>The entries of a backup in a fixed order, so the same content always gives the same hash.</summary>
    private static List<(string Name, byte[] Bytes)> Serialize(BackupContent content)
    {
        var entries = new List<(string, byte[])>();
        foreach (var profile in content.Profiles.OrderBy(p => p.Id, StringComparer.Ordinal))
            entries.Add(($"profiles/{profile.Id}.json", JsonSerializer.SerializeToUtf8Bytes(profile, Json)));
        entries.Add((TreeEntry, JsonSerializer.SerializeToUtf8Bytes(content.ProfileTree, Json)));
        if (content.Preferences is not null) entries.Add((PreferencesEntry, JsonSerializer.SerializeToUtf8Bytes(content.Preferences, Json)));
        entries.Add((VariablesEntry, JsonSerializer.SerializeToUtf8Bytes(content.Variables, Json)));
        entries.Add((DevicesEntry, JsonSerializer.SerializeToUtf8Bytes(content.Devices.OrderBy(d => d.Id, StringComparer.Ordinal), Json)));
        foreach (var (id, settings) in content.PluginSettings.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
            entries.Add(($"plugin-settings/{id}.json", Encoding.UTF8.GetBytes(settings.ToJsonString(Json))));
        foreach (var (tag, text) in content.LanguagePacks.OrderBy(p => p.Key, StringComparer.Ordinal))
            entries.Add(($"languages/{tag}.json", Encoding.UTF8.GetBytes(text)));
        return entries;
    }

    private static string Hash(List<(string Name, byte[] Bytes)> entries)
    {
        using var sha = SHA256.Create();
        foreach (var (name, bytes) in entries)
        {
            var label = Encoding.UTF8.GetBytes(name + "\n" + bytes.Length + "\n");
            sha.TransformBlock(label, 0, label.Length, null, 0);
            sha.TransformBlock(bytes, 0, bytes.Length, null, 0);
        }
        sha.TransformFinalBlock([], 0, 0);
        return Convert.ToHexString(sha.Hash!).ToLowerInvariant();
    }

    private static void WriteEntry(ZipArchive zip, string name, byte[] content)
    {
        using var stream = zip.CreateEntry(name, CompressionLevel.Optimal).Open();
        stream.Write(content);
    }

    private static byte[] ReadEntry(ZipArchiveEntry entry)
    {
        if (entry.Length > MaxEntryBytes) throw new InvalidDataException("The backup is too large.");
        using var stream = entry.Open();
        using var output = new MemoryStream();
        // The declared length can lie in a crafted zip, so the read itself is bounded too.
        var buffer = new byte[81920];
        int read;
        while ((read = stream.Read(buffer)) > 0)
        {
            output.Write(buffer, 0, read);
            if (output.Length > MaxEntryBytes) throw new InvalidDataException("The backup is too large.");
        }
        return output.ToArray();
    }
}
