namespace MacroGrid.Core.Plugins.Images;

/// <summary>The rules a picture from a plugin must pass before the host keeps it: a raster type, at most 256 KB decoded, a header that says at most 1024 x 1024 pixels, and bytes that really are that type.
/// The server never decodes a picture: only the few header bytes that hold the size (and, for a GIF, the block structure) are read. EXIF and other metadata pass through unchanged and are never parsed here.</summary>
public static class PluginImageValidator
{
    public const int MaxDecodedBytes = 256 * 1024;
    public const int MaxSide = 1024;
    public const int MaxPixels = 1024 * 1024;
    public const int MaxGifFrames = 64;
    private const int MaxJpegMarkers = 64;

    /// <summary>The longest data URL that can still decode to <see cref="MaxDecodedBytes"/>: checked before any decoding, so a huge string costs nothing.</summary>
    private const int MaxDataUrlLength = 32 + (MaxDecodedBytes + 2) / 3 * 4;

    private static readonly string[] Types = ["png", "jpeg", "webp", "gif"];

    // The first chunk of a PNG: length 13, type IHDR.
    private static ReadOnlySpan<byte> IhdrStart => [0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52];

    private static ReadOnlySpan<byte> PngMagic => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public enum Failure { None, NotADataUrl, UnsupportedType, TooLarge, BadBase64, ContentMismatch, TooManyPixels, Unreadable }

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

        if (!TryReadSize(buffer.AsSpan(0, written), actual, out var width, out var height)) return Failure.Unreadable;
        if (width is 0 or > MaxSide || height is 0 or > MaxSide || (long)width * height > MaxPixels) return Failure.TooManyPixels;

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

    /// <summary>Reads the picture's size from its header only (see the class summary). False when the header is missing, cut off or malformed, and for a GIF
    /// also when it has more than <see cref="MaxGifFrames"/> frames or its block structure does not hold together.</summary>
    public static bool TryReadSize(ReadOnlySpan<byte> data, string type, out int width, out int height)
    {
        width = height = 0;
        return type switch
        {
            "png" => ReadPng(data, out width, out height),
            "gif" => ReadGif(data, out width, out height),
            "jpeg" => ReadJpeg(data, out width, out height),
            "webp" => ReadWebp(data, out width, out height),
            _ => false,
        };
    }

    private static int Be16(ReadOnlySpan<byte> d, int at) => d[at] << 8 | d[at + 1];
    private static int Le16(ReadOnlySpan<byte> d, int at) => d[at] | d[at + 1] << 8;

    private static bool ReadPng(ReadOnlySpan<byte> d, out int w, out int h)
    {
        w = h = 0;
        // The first chunk must be IHDR (length 13): 8 magic bytes, 4 length, 4 type, then width and height.
        if (d.Length < 24 || !d.Slice(8, 8).SequenceEqual(IhdrStart)) return false;
        var width = (long)d[16] << 24 | (long)d[17] << 16 | (long)d[18] << 8 | d[19];
        var height = (long)d[20] << 24 | (long)d[21] << 16 | (long)d[22] << 8 | d[23];
        w = (int)Math.Min(width, int.MaxValue);
        h = (int)Math.Min(height, int.MaxValue);
        return true;
    }

    private static bool ReadJpeg(ReadOnlySpan<byte> d, out int w, out int h)
    {
        w = h = 0;
        var pos = 2;
        for (var markers = 0; markers < MaxJpegMarkers; markers++)
        {
            while (pos < d.Length && d[pos] == 0xFF && pos + 1 < d.Length && d[pos + 1] == 0xFF) pos++; // fill bytes
            if (pos + 1 >= d.Length || d[pos] != 0xFF) return false;
            var marker = d[pos + 1];
            if (marker is 0xC0 or 0xC1 or 0xC2)
            {
                if (pos + 9 > d.Length) return false;
                h = Be16(d, pos + 5);
                w = Be16(d, pos + 7);
                return true;
            }
            if (marker is 0xD9 or 0xDA) return false; // the image data starts or ends with no frame header before it
            if (marker is 0x01 or 0xD8 or (>= 0xD0 and <= 0xD7)) { pos += 2; continue; } // markers with no length
            if (pos + 4 > d.Length) return false;
            var length = Be16(d, pos + 2);
            if (length < 2) return false;
            pos += 2 + length;
        }
        return false;
    }

    private static bool ReadWebp(ReadOnlySpan<byte> d, out int w, out int h)
    {
        w = h = 0;
        if (d.Length < 30) return false;
        var chunk = d.Slice(12, 4);
        if (chunk.SequenceEqual("VP8 "u8))
        {
            if (d[23] != 0x9D || d[24] != 0x01 || d[25] != 0x2A) return false;
            w = Le16(d, 26) & 0x3FFF;
            h = Le16(d, 28) & 0x3FFF;
            return true;
        }
        if (chunk.SequenceEqual("VP8L"u8))
        {
            if (d[20] != 0x2F) return false;
            var bits = d[21] | d[22] << 8 | d[23] << 16 | (uint)d[24] << 24;
            w = (int)(bits & 0x3FFF) + 1;
            h = (int)(bits >> 14 & 0x3FFF) + 1;
            return true;
        }
        if (chunk.SequenceEqual("VP8X"u8))
        {
            w = (d[24] | d[25] << 8 | d[26] << 16) + 1;
            h = (d[27] | d[28] << 8 | d[29] << 16) + 1;
            return true;
        }
        return false;
    }

    private static bool ReadGif(ReadOnlySpan<byte> d, out int w, out int h)
    {
        w = h = 0;
        if (d.Length < 13) return false;
        w = Le16(d, 6);
        h = Le16(d, 8);
        var pos = 13;
        if ((d[10] & 0x80) != 0) pos += 3 << ((d[10] & 7) + 1);

        var frames = 0;
        while (pos < d.Length)
        {
            var block = d[pos++];
            switch (block)
            {
                case 0x3B: // trailer: the structure holds together
                    return frames > 0;
                case 0x21: // extension: a label, then data sub-blocks
                    if (pos >= d.Length) return false;
                    pos++;
                    if (!SkipSubBlocks(d, ref pos)) return false;
                    break;
                case 0x2C: // image descriptor
                    if (++frames > MaxGifFrames || pos + 9 > d.Length) return false;
                    var packed = d[pos + 8];
                    pos += 9;
                    if ((packed & 0x80) != 0) pos += 3 << ((packed & 7) + 1);
                    if (pos >= d.Length) return false;
                    pos++; // LZW minimum code size
                    if (!SkipSubBlocks(d, ref pos)) return false;
                    break;
                default:
                    return false;
            }
        }
        return false; // no trailer: cut off
    }

    private static bool SkipSubBlocks(ReadOnlySpan<byte> d, ref int pos)
    {
        while (pos < d.Length)
        {
            var size = d[pos++];
            if (size == 0) return true;
            pos += size;
        }
        return false;
    }

    public static string ToDataUrl(ReadOnlySpan<byte> bytes) =>
        $"data:image/{Sniff(bytes) ?? "png"};base64,{Convert.ToBase64String(bytes)}";
}
