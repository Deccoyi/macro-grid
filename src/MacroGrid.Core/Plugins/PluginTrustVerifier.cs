using System.Security.Cryptography;
using System.Text.Json;
using MacroGrid.Core.Plugins.Distribution;
using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Core.Plugins;

/// <summary>What <see cref="PluginTrustVerifier.Verify"/> found. <see cref="SignedFiles"/> maps every file the signature
/// lists (relative path with '/') to its SHA-256 (lowercase hex); it is null for an unsigned plugin that a development
/// build lets through, which is then not restricted to a file list.</summary>
public sealed record PluginTrustResult(bool Allowed, bool Unsigned, string? Reason, IReadOnlyDictionary<string, string>? SignedFiles)
{
    internal static PluginTrustResult Trusted(IReadOnlyDictionary<string, string> files) => new(true, false, null, files);
    internal static PluginTrustResult Refused(string reason) => new(false, false, reason, null);
}

/// <summary>
/// Decides whether a C# plugin folder may run. Only official C# plugins do: the folder must carry
/// <c>signature.json</c> and <c>signature.sig</c> made with the official plugin-signing key (see
/// scripts/sign-package-contents.cs in the plugin repository), and every file must still match. It runs every time a C#
/// plugin loads (start, install, reload, update), never only at download, so a file changed on disk afterwards is caught.
/// <list type="number">
/// <item>The signature verifies over the exact bytes of <c>signature.json</c> with the official public key.</item>
/// <item><c>id</c>, <c>version</c> and <c>kind</c> in <c>signature.json</c> equal those in <c>plugin.json</c>.</item>
/// <item>Every listed file exists and its SHA-256 matches.</item>
/// <item>No file that is not listed has a loadable or executable extension. Other unlisted files are fine: a plugin's
/// own data (settings and the like) lives in the same folder.</item>
/// </list>
/// A development build (<c>MACROGRID_UNSIGNED_PLUGINS</c>, defined only in the Debug configuration) lets a plugin that fails
/// these checks load anyway and marks it unsigned. Released builds are Release builds; nothing at run time turns it on.
/// </summary>
public sealed class PluginTrustVerifier
{
    public const string SignatureJsonName = "signature.json";
    public const string SignatureName = "signature.sig";

    public const string NotOfficialReason = "Only official C# plugins can run. Third-party plugins must be JavaScript.";
    public const string MismatchReason = "The plugin's files do not match its signature.";

    /// <summary>True only in a build made from source in the Debug configuration.</summary>
#if MACROGRID_UNSIGNED_PLUGINS
    public const bool DevelopmentBuild = true;
#else
    public const bool DevelopmentBuild = false;
#endif

    // Files that can be loaded or run. A plugin cannot add one of these to its folder after it was signed.
    private static readonly HashSet<string> ExecutableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".dll", ".exe", ".so", ".dylib", ".node", ".ps1", ".bat", ".cmd", ".com", ".scr", ".msi", ".js", ".vbs", ".wsf", ".hta", ".lnk",
    };

    private readonly byte[] _publicKey;
    private readonly bool _allowUnsigned;

    /// <summary>The official key, unsigned loading only in a development build.</summary>
    public static PluginTrustVerifier Official { get; } = new(Convert.FromBase64String(PluginSigning.PublicKeyBase64), DevelopmentBuild);

    /// <summary>For tests: any public key (SubjectPublicKeyInfo, DER) and an explicit unsigned setting.</summary>
    internal PluginTrustVerifier(byte[] publicKey, bool allowUnsigned)
    {
        _publicKey = publicKey;
        _allowUnsigned = allowUnsigned;
    }

    /// <summary>True when an unsigned C# plugin would still be loaded (a development build).</summary>
    public bool AllowsUnsigned => _allowUnsigned;

    public PluginTrustResult Verify(string dir, PluginManifest manifest)
    {
        var strict = VerifyStrict(dir, manifest);
        if (strict.Allowed || !_allowUnsigned) return strict;
        return new PluginTrustResult(true, true, strict.Reason, null);
    }

    private PluginTrustResult VerifyStrict(string dir, PluginManifest manifest)
    {
        var jsonPath = Path.Combine(dir, SignatureJsonName);
        var sigPath = Path.Combine(dir, SignatureName);
        if (!File.Exists(jsonPath) || !File.Exists(sigPath))
            return PluginTrustResult.Refused(NotOfficialReason);

        try
        {
            var jsonBytes = File.ReadAllBytes(jsonPath);
            var signature = Convert.FromBase64String(File.ReadAllText(sigPath).Trim());
            using (var ecdsa = ECDsa.Create())
            {
                ecdsa.ImportSubjectPublicKeyInfo(_publicKey, out _);
                if (!ecdsa.VerifyData(jsonBytes, signature, HashAlgorithmName.SHA256))
                    return PluginTrustResult.Refused(NotOfficialReason);
            }

            using var doc = JsonDocument.Parse(jsonBytes);
            var root = doc.RootElement;
            if (root.GetProperty("formatVersion").GetInt32() != 1
                || root.GetProperty("id").GetString() != manifest.Id
                || root.GetProperty("version").GetString() != manifest.Version
                || !string.Equals(root.GetProperty("kind").GetString(), manifest.Kind.ToString(), StringComparison.OrdinalIgnoreCase))
                return PluginTrustResult.Refused(MismatchReason);

            var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in root.GetProperty("files").EnumerateArray())
            {
                var path = entry.GetProperty("path").GetString();
                var sha256 = entry.GetProperty("sha256").GetString();
                if (!IsSafeRelativePath(path) || string.IsNullOrEmpty(sha256) || !files.TryAdd(path!, sha256.ToLowerInvariant()))
                    return PluginTrustResult.Refused(MismatchReason);
            }

            var rootFull = Path.GetFullPath(dir);
            foreach (var (path, sha256) in files)
            {
                var full = Path.GetFullPath(Path.Combine(rootFull, path));
                if (!full.StartsWith(rootFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    return PluginTrustResult.Refused(MismatchReason);
                if (!File.Exists(full))
                    return PluginTrustResult.Refused(MismatchReason);
                if (!string.Equals(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(full))), sha256, StringComparison.OrdinalIgnoreCase))
                    return PluginTrustResult.Refused(MismatchReason);
            }

            foreach (var file in Directory.EnumerateFiles(rootFull, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(rootFull, file).Replace('\\', '/');
                if (relative is SignatureJsonName or SignatureName || files.ContainsKey(relative)) continue;
                if (ExecutableExtensions.Contains(Path.GetExtension(relative)))
                    return PluginTrustResult.Refused(MismatchReason);
            }

            return PluginTrustResult.Trusted(files);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException or FormatException
                                       or CryptographicException or IOException or UnauthorizedAccessException)
        {
            return PluginTrustResult.Refused(MismatchReason);
        }
    }

    private static bool IsSafeRelativePath(string? path) =>
        !string.IsNullOrEmpty(path) && !Path.IsPathRooted(path) && !path.Contains('\\')
        && !path.Split('/').Any(part => part is "" or "." or "..");
}
