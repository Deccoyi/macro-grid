using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MacroGrid.Core.Plugins.Distribution;

namespace MacroGrid.Core.Languages;

/// <summary>One installed language pack as the list shows it.</summary>
public sealed record LanguagePackInfo(string Tag, string Name, int Version);

/// <summary>A pack or a CSV file that was refused; the message is meant for the person.</summary>
public sealed class LanguagePackException(string message) : Exception(message);

/// <summary>
/// Keeps language packs as one JSON file each in <c>languages/&lt;tag&gt;.json</c> of the data folder. The server only checks the container
/// (the tag, the size and the shape of the JSON) and never interprets the strings: the editor cleans and checks every string again each
/// time it loads a pack, because the file can also be edited by hand. Write-then-rename like <see cref="Preferences.PreferencesStore"/>.
/// </summary>
public sealed partial class LanguagePackStore
{
    public const int MaxBytes = 1024 * 1024;
    public const int MaxEntries = 6000;
    public const int MaxPacks = 20;
    public const int MaxNameLength = 40;
    public const int MaxTagLength = 35;

    private static readonly UTF8Encoding Utf8 = new(false);

    private readonly string _dir;
    private readonly Lock _lock = new();

    public LanguagePackStore(string dataDir)
    {
        _dir = Path.Combine(dataDir, "languages");
    }

    /// <summary>A BCP 47 style tag with a lower-case language: <c>de</c>, <c>pt-BR</c>, <c>zh-Hant</c>. The built-in languages cannot be replaced by a pack.
    /// The tag becomes a file name, so this check is also the path check.</summary>
    public static bool IsValidTag(string? tag) =>
        tag is { Length: >= 2 and <= MaxTagLength }
        && TagPattern().IsMatch(tag)
        && !tag.Equals("tr", StringComparison.Ordinal) && !tag.Equals("en", StringComparison.Ordinal);

    public IReadOnlyList<LanguagePackInfo> List()
    {
        lock (_lock)
        {
            if (!Directory.Exists(_dir)) return [];
            var packs = new List<LanguagePackInfo>();
            foreach (var file in Directory.EnumerateFiles(_dir, "*.json").Order(StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    if (new FileInfo(file).Length > MaxBytes) continue;
                    using var document = JsonDocument.Parse(File.ReadAllBytes(file));
                    var meta = document.RootElement.GetProperty("meta");
                    var tag = meta.GetProperty("tag").GetString();
                    if (!IsValidTag(tag) || !FileName(tag!).Equals(Path.GetFileName(file), StringComparison.OrdinalIgnoreCase)) continue;
                    var name = meta.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? CatalogText.Clean(n.GetString(), MaxNameLength) : null;
                    var version = meta.TryGetProperty("version", out var v) && v.TryGetInt32(out var parsed) ? parsed : 1;
                    packs.Add(new LanguagePackInfo(tag!, name ?? tag!, version));
                }
                catch (Exception ex) when (ex is JsonException or IOException or InvalidOperationException or KeyNotFoundException)
                {
                    // A damaged file is left out of the list; nothing else reads it either.
                }
            }
            return packs;
        }
    }

    /// <summary>The stored JSON text of a pack, or null when there is none.</summary>
    public string? Read(string tag)
    {
        if (!IsValidTag(tag)) return null;
        lock (_lock)
        {
            var path = Path.Combine(_dir, FileName(tag));
            try
            {
                return File.Exists(path) && new FileInfo(path).Length <= MaxBytes ? File.ReadAllText(path, Utf8) : null;
            }
            catch (IOException)
            {
                return null;
            }
        }
    }

    /// <summary>Checks the container and stores it. Throws <see cref="LanguagePackException"/> when the tag, the size or the shape is wrong.</summary>
    public void Save(string tag, string json)
    {
        if (!IsValidTag(tag)) throw new LanguagePackException("That language tag is not allowed.");
        if (Utf8.GetByteCount(json) > MaxBytes) throw new LanguagePackException("The language pack is larger than 1 MB.");
        CheckContainer(tag, json);

        lock (_lock)
        {
            Directory.CreateDirectory(_dir);
            var path = Path.Combine(_dir, FileName(tag));
            if (!File.Exists(path) && Directory.EnumerateFiles(_dir, "*.json").Count() >= MaxPacks)
                throw new LanguagePackException($"At most {MaxPacks} language packs can be installed. Remove one first.");

            var tmp = path + ".tmp";
            File.WriteAllText(tmp, json, Utf8);
            File.Move(tmp, path, overwrite: true);
        }
    }

    public bool Delete(string tag)
    {
        if (!IsValidTag(tag)) return false;
        lock (_lock)
        {
            var path = Path.Combine(_dir, FileName(tag));
            if (!File.Exists(path)) return false;
            File.Delete(path);
            return true;
        }
    }

    private static void CheckContainer(string tag, string json)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 8 });
        }
        catch (JsonException)
        {
            throw new LanguagePackException("The language pack is not valid JSON.");
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("meta", out var meta) || meta.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("strings", out var strings) || strings.ValueKind != JsonValueKind.Object)
                throw new LanguagePackException("The language pack needs a \"meta\" and a \"strings\" object.");

            if (!meta.TryGetProperty("tag", out var metaTag) || metaTag.ValueKind != JsonValueKind.String
                || !string.Equals(metaTag.GetString(), tag, StringComparison.OrdinalIgnoreCase))
                throw new LanguagePackException("The tag inside the language pack does not match its name.");

            var count = 0;
            foreach (var entry in strings.EnumerateObject())
            {
                if (entry.Value.ValueKind != JsonValueKind.String) throw new LanguagePackException("Every string of a language pack must be text.");
                if (++count > MaxEntries) throw new LanguagePackException($"A language pack can have at most {MaxEntries} strings.");
            }
        }
    }

    /// <summary>Lower case, so two tags that differ only in case cannot become two files on a case-insensitive disk.</summary>
    private static string FileName(string tag) => tag.ToLowerInvariant() + ".json";

    [GeneratedRegex("^[a-z]{2,3}(-[A-Za-z0-9]{2,8}){0,3}$")]
    private static partial Regex TagPattern();
}

/// <summary>The two CSV files of the language screen: reading one chosen by the person and preparing one to save. Text only; nothing is interpreted.</summary>
public static class LanguageCsvFile
{
    public const int MaxImportBytes = 1024 * 1024;
    public const int MaxExportBytes = 2 * 1024 * 1024;

    private static readonly UTF8Encoding Strict = new(false, throwOnInvalidBytes: true);

    /// <summary>Reads a file as text. The size is checked before anything is read, and bytes that are not valid UTF-8 are refused.</summary>
    public static string ReadText(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists) throw new LanguagePackException("The file was not found.");
        if (info.Length > MaxImportBytes) throw new LanguagePackException("The file is larger than 1 MB.");
        try
        {
            return Strict.GetString(File.ReadAllBytes(path));
        }
        catch (DecoderFallbackException)
        {
            throw new LanguagePackException("The file is not UTF-8 text. Save it as CSV UTF-8 and try again.");
        }
    }

    /// <summary>The bytes to write (UTF-8 with a byte order mark, so a spreadsheet program reads the characters right) and a file name that is safe to suggest.</summary>
    public static (string FileName, byte[] Bytes) PrepareExport(string? fileName, string? text)
    {
        if (text is null) throw new LanguagePackException("There is nothing to save.");
        byte[] body;
        try
        {
            body = Strict.GetBytes(text);
        }
        catch (EncoderFallbackException)
        {
            throw new LanguagePackException("The text has characters that cannot be saved.");
        }
        if (body.Length > MaxExportBytes) throw new LanguagePackException("The file would be larger than 2 MB.");

        var invalid = Path.GetInvalidFileNameChars();
        var clean = string.Concat((fileName ?? "").Select(c => invalid.Contains(c) || char.IsControl(c) ? '_' : c)).Trim().TrimEnd('.');
        if (clean.Length > 80) clean = clean[..80];
        if (clean.Length == 0) clean = "language";
        if (!clean.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)) clean += ".csv";

        var bytes = new byte[body.Length + 3];
        bytes[0] = 0xEF;
        bytes[1] = 0xBB;
        bytes[2] = 0xBF;
        body.CopyTo(bytes, 3);
        return (clean, bytes);
    }
}
