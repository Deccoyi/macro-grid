using System.Text.Json;

namespace MacroGrid.Core.Plugins.Distribution;

/// <summary>What a verified catalog file says in its header. <see cref="Payload"/> is the exact signed bytes, parsed only after the signature held.</summary>
public sealed record SignedCatalogDocument(string Kind, long Sequence, DateTimeOffset? IssuedAt, byte[] Payload);

/// <summary>
/// Reads one of the official catalog's signed files: an envelope <c>{ "payload": "&lt;base64&gt;", "signature": "&lt;base64&gt;" }</c> whose signature
/// covers the decoded payload bytes (ECDSA P-256, SHA-256, the scheme of a package signature). The payload is base64 so nothing that touches
/// the file (line endings, an editor) can change a signed byte, and it is parsed only after verification. The payload starts with a header:
/// <c>kind</c> ("index" or "revoked"), <c>sequence</c> (a whole number from 1, raised on every signing) and <c>issuedAt</c> (UTC, informational).
/// Never throws on bad input.
/// </summary>
public static class SignedCatalogFile
{
    public const string IndexKind = "index";
    public const string RevokedKind = "revoked";

    public static bool TryRead(byte[] envelope, string expectedKind, byte[] publicKey, out SignedCatalogDocument document, long maxBytes = 2 * 1024 * 1024)
    {
        document = null!;
        if (envelope.Length == 0 || envelope.Length > maxBytes) return false;
        try
        {
            string? payloadText, signature;
            using (var envelopeJson = JsonDocument.Parse(envelope))
            {
                var root = envelopeJson.RootElement;
                if (root.ValueKind != JsonValueKind.Object) return false;
                payloadText = Text(root, "payload");
                signature = Text(root, "signature");
            }
            if (payloadText is null || signature is null) return false;

            var payload = Convert.FromBase64String(payloadText);
            if (!PluginSigning.Verify(payload, signature, publicKey)) return false;

            using var payloadJson = JsonDocument.Parse(payload);
            var header = payloadJson.RootElement;
            if (header.ValueKind != JsonValueKind.Object) return false;
            if (Text(header, "kind") != expectedKind) return false;
            if (!header.TryGetProperty("sequence", out var seq) || !seq.TryGetInt64(out var sequence) || sequence < 1) return false;
            DateTimeOffset? issuedAt = Text(header, "issuedAt") is { } at && DateTimeOffset.TryParse(at, null, System.Globalization.DateTimeStyles.AssumeUniversal, out var parsed)
                ? parsed
                : null;

            document = new SignedCatalogDocument(expectedKind, sequence, issuedAt, payload);
            return true;
        }
        catch (Exception ex) when (ex is JsonException or FormatException or InvalidOperationException)
        {
            return false;
        }
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
