using System.IO.Compression;
using System.Text.Json;
using MacroStation.Core.Model;
using MacroStation.Protocol;

namespace MacroStation.Core.Profiles;

/// <summary>A plugin a packaged profile needs, and the action types of it that the profile uses.</summary>
public sealed record PackagePluginRef(string Id, string Name, string Version, IReadOnlyList<string> ActionTypes);

/// <param name="FormatVersion">The package layout version; a package with a newer one is refused, not guessed at.</param>
public sealed record ProfilePackageManifest(
    int FormatVersion,
    string ProfileName,
    DateTimeOffset ExportedAt,
    string ServerVersion,
    IReadOnlyList<PackagePluginRef> RequiredPlugins);

public sealed record ProfilePackageContent(Profile Profile, ProfilePackageManifest? Manifest);

/// <summary>
/// The <c>.msprofile</c> file: a zip holding <c>manifest.json</c> (format version, who exported it, which plugins
/// the profile's actions need) and <c>profile.json</c> (the profile itself). Icons and images are stored inside
/// <c>profile.json</c> as the <c>data:</c> values the profile already holds, so a package is self-contained and
/// needs no separate asset entries. Reading is defensive: only the two known entries are read, with a size cap,
/// and the profile must pass <see cref="ProfileValidator"/>. A plain profile JSON file (the older export) is
/// accepted too, just without a manifest.
/// </summary>
public static class ProfilePackage
{
    public const int CurrentFormatVersion = 1;
    public const string Extension = ".msprofile";
    private const string ManifestEntry = "manifest.json";
    private const string ProfileEntry = "profile.json";
    private const long MaxEntryBytes = 64 * 1024 * 1024;

    private static readonly JsonSerializerOptions Json = new(ProtocolJson.Options) { WriteIndented = true };

    /// <summary>Every distinct action type bound anywhere in the profile.</summary>
    public static IReadOnlyList<string> ActionTypes(Profile profile) =>
    [
        .. profile.Pages
            .SelectMany(p => p.Widgets)
            .SelectMany(w => w.Actions.Values)
            .SelectMany(bindings => bindings)
            .Select(b => b.Type)
            .Distinct(StringComparer.OrdinalIgnoreCase)
    ];

    public static byte[] Write(Profile profile, ProfilePackageManifest manifest)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(zip, ManifestEntry, JsonSerializer.SerializeToUtf8Bytes(manifest, Json));
            WriteEntry(zip, ProfileEntry, JsonSerializer.SerializeToUtf8Bytes(profile, Json));
        }
        return buffer.ToArray();
    }

    /// <exception cref="InvalidDataException">The file is not a usable profile; the message says why.</exception>
    public static ProfilePackageContent Read(byte[] bytes)
    {
        if (LooksLikeZip(bytes))
            return ReadPackage(bytes);

        // The older plain-JSON export.
        return new ProfilePackageContent(ParseProfile(bytes), null);
    }

    private static ProfilePackageContent ReadPackage(byte[] bytes)
    {
        try
        {
            using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);

            ProfilePackageManifest? manifest = null;
            if (zip.GetEntry(ManifestEntry) is { } manifestEntry)
            {
                try { manifest = JsonSerializer.Deserialize<ProfilePackageManifest>(ReadEntry(manifestEntry), Json); }
                catch (JsonException ex) { throw new InvalidDataException("The package manifest is damaged.", ex); }
            }

            if (manifest is { FormatVersion: > CurrentFormatVersion })
                throw new InvalidDataException($"This profile was made by a newer version (package format {manifest.FormatVersion}); update Macro Station to open it.");

            var profileEntry = zip.GetEntry(ProfileEntry) ?? throw new InvalidDataException("The package has no profile.");
            return new ProfilePackageContent(ParseProfile(ReadEntry(profileEntry)), manifest);
        }
        catch (InvalidDataException) { throw; }
        catch (Exception ex) when (ex is IOException or NotSupportedException)
        {
            throw new InvalidDataException("The file could not be read as a profile package.", ex);
        }
    }

    private static Profile ParseProfile(byte[] json)
    {
        Profile? profile;
        try { profile = JsonSerializer.Deserialize<Profile>(json, ProtocolJson.Options); }
        catch (JsonException ex) { throw new InvalidDataException("The profile is damaged.", ex); }

        if (profile is null) throw new InvalidDataException("The profile is empty.");
        if (!ProfileValidator.Validate(profile, out var error))
            throw new InvalidDataException(error ?? "The profile is not valid.");
        return profile;
    }

    private static void WriteEntry(ZipArchive zip, string name, byte[] content)
    {
        using var stream = zip.CreateEntry(name, CompressionLevel.Optimal).Open();
        stream.Write(content);
    }

    private static byte[] ReadEntry(ZipArchiveEntry entry)
    {
        if (entry.Length > MaxEntryBytes)
            throw new InvalidDataException("The package is too large.");
        using var stream = entry.Open();
        using var output = new MemoryStream();
        // The declared length can lie in a crafted zip, so the read itself is bounded too.
        var buffer = new byte[81920];
        int read;
        while ((read = stream.Read(buffer)) > 0)
        {
            output.Write(buffer, 0, read);
            if (output.Length > MaxEntryBytes) throw new InvalidDataException("The package is too large.");
        }
        return output.ToArray();
    }

    private static bool LooksLikeZip(byte[] bytes) => bytes.Length > 3 && bytes[0] == 'P' && bytes[1] == 'K';
}
