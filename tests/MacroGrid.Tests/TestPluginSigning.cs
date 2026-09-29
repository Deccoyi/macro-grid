using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MacroGrid.Core.Plugins;

namespace MacroGrid.Tests;

/// <summary>A throwaway signing key for tests, never the official one. <see cref="Sign"/> writes what
/// scripts/sign-package-contents.cs in the plugin repository writes: signature.json and signature.sig.</summary>
internal static class TestPluginSigning
{
    private static readonly ECDsa Key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    public static byte[] PublicKey { get; } = Key.ExportSubjectPublicKeyInfo();

    /// <summary>Checks signatures against the test key and refuses everything else, like a released build.</summary>
    public static PluginTrustVerifier Strict => new(PublicKey, allowUnsigned: false);

    /// <summary>Like a development build: an unsigned C# plugin still loads (for tests that are not about trust).</summary>
    public static PluginTrustVerifier Lenient => new(PublicKey, allowUnsigned: true);

    /// <summary>Signs a folder that already holds its plugin.json. The header fields are overridable to test mismatches.</summary>
    public static void Sign(string dir, string? id = null, string? version = null, string? kind = null, ECDsa? key = null)
    {
        using var manifest = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(dir, "plugin.json")));
        var root = manifest.RootElement;
        string Field(string name) => root.GetProperty(name).GetString()!;

        var files = new List<(string Path, string Sha256)>();
        foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(dir, file).Replace('\\', '/');
            if (relative is "signature.json" or "signature.sig") continue;
            files.Add((relative, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))).ToLowerInvariant()));
        }
        files.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path));

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("formatVersion", 1);
            writer.WriteString("id", id ?? Field("id"));
            writer.WriteString("version", version ?? Field("version"));
            writer.WriteString("kind", kind ?? Field("kind"));
            writer.WriteStartArray("files");
            foreach (var (path, sha) in files)
            {
                writer.WriteStartObject();
                writer.WriteString("path", path);
                writer.WriteString("sha256", sha);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        var bytes = stream.ToArray();
        File.WriteAllBytes(Path.Combine(dir, "signature.json"), bytes);
        File.WriteAllText(Path.Combine(dir, "signature.sig"), Convert.ToBase64String((key ?? Key).SignData(bytes, HashAlgorithmName.SHA256)), new UTF8Encoding(false));
    }

    /// <summary>A key that is not the test key, for a signature by someone else.</summary>
    public static ECDsa NewOtherKey() => ECDsa.Create(ECCurve.NamedCurves.nistP256);
}
