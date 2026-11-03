namespace MacroGrid.Core.Plugins.Images;

/// <summary>The rules a picture from a plugin must pass before the host keeps it: a raster type, at most 64 KB decoded, and bytes that really are that type.</summary>
public static class PluginImageValidator
{
    public const int MaxDecodedBytes = 64 * 1024;

    /// <summary>The longest data URL that can still decode to <see cref="MaxDecodedBytes"/>: checked before any decoding, so a huge string costs nothing.</summary>
    private const int MaxDataUrlLength = 32 + (MaxDecodedBytes + 2) / 3 * 4;

    private static readonly string[] Types = ["png", "jpeg", "webp", "gif"];

    private static ReadOnlySpan<byte> PngMagic => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public enum Failure { None, NotADataUrl, UnsupportedType, TooLarge, BadBase64, ContentMismatch }

    /// <summary>Checks a data URL and returns the decoded bytes and the media type taken from the bytes, never from the claim alone.</summary>
    public static Failure TryValidate(string? dataUrl, out byte[] bytes, out string mediaType)
    {
        bytes = [];
        mediaType = "";
        if (dataUrl is null || !dataUrl.StartsWith("data:image/", StringComparison.Ordinal)) return Failure.NotADataUrl;
        if (dataUrl.Length > MaxDataUrlLength) return Failure.TooLarge;

        var semicolon = dataUrl.IndexOf(';');
        if (semicolon < 0 || !dataUrl.AsSpan(semicolon).StartsWith(";base64,", StringComparison.Ordinal)) return Failure.NotADataUrl;
        var declared = dataUrl["data:image/".Length..semicolon];
        if (!Types.Contains(declared, StringComparer.Ordinal)) return Failure.UnsupportedType;

        var payload = dataUrl.AsSpan(semicolon + ";base64,".Length);
        var buffer = new byte[payload.Length / 4 * 3 + 3];
        if (!Convert.TryFromBase64Chars(payload, buffer, out var written)) return Failure.BadBase64;
        if (written is 0 or > MaxDecodedBytes) return written == 0 ? Failure.BadBase64 : Failure.TooLarge;

        var actual = Sniff(buffer.AsSpan(0, written));
        if (actual != declared) return Failure.ContentMismatch;

        bytes = buffer[..written];
        mediaType = "image/" + actual;
        return Failure.None;
    }

    /// <summary>The image type the first bytes say, or null when they are none of the four allowed types.</summary>
    public static string? Sniff(ReadOnlySpan<byte> data)
    {
        if (data.StartsWith(PngMagic)) return "png";
        if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF) return "jpeg";
        if (data.StartsWith("GIF87a"u8) || data.StartsWith("GIF89a"u8)) return "gif";
        if (data.Length >= 12 && data[..4].SequenceEqual("RIFF"u8) && data.Slice(8, 4).SequenceEqual("WEBP"u8)) return "webp";
        return null;
    }

    public static string ToDataUrl(ReadOnlySpan<byte> bytes) =>
        $"data:image/{Sniff(bytes) ?? "png"};base64,{Convert.ToBase64String(bytes)}";
}
