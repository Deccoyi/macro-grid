using System.Net;

namespace MacroGrid.Core.Plugins.Distribution;

/// <summary>Why a plugin package could not be downloaded and installed. <see cref="Code"/> is stable so the editor
/// can show a translated message.</summary>
public sealed class PluginDownloadException(string code, string message, Exception? inner = null) : Exception(message, inner)
{
    /// <summary>The URL (or a redirect hop) is not on an allowed host.</summary>
    public const string Refused = "refused";

    /// <summary>The network or the disk failed.</summary>
    public const string Failed = "failed";

    /// <summary>Wrong size or wrong SHA-256 against what the catalog said.</summary>
    public const string Verify = "verify";

    /// <summary>Official source only: the signature does not verify against the embedded public key.</summary>
    public const string Signature = "signature";

    public string Code { get; } = code;
}

/// <summary>
/// Downloads one plugin package into <c>%AppData%\MacroGrid\plugins-staging\&lt;guid&gt;\</c>, verifying size, SHA-256
/// and — for the official source — the signature, before anything is unzipped. Every URL, including each redirect
/// hop, must pass <see cref="PluginSourceUrls.IsAllowedUrl"/>; the <see cref="HttpClient"/> handed in must have
/// automatic redirects switched off so this class can check each hop itself (same shape as
/// MacroGrid.Core.Updates.InstallerDownloader, which does the same for the server installer).
/// </summary>
public sealed class PluginPackageDownloader(HttpClient http)
{
    private const int MaxRedirects = 5;
    private const long MaxPackageBytes = 100L * 1024 * 1024;

    /// <returns>The downloaded package's bytes, already verified.</returns>
    public async Task<byte[]> DownloadAsync(PluginCatalogVersion version, bool requireOfficialSignature, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(version.Url, UriKind.Absolute, out var url) || !PluginSourceUrls.IsAllowedUrl(url))
            throw new PluginDownloadException(PluginDownloadException.Refused, "The download address is not allowed.");
        if (version.Size > MaxPackageBytes)
            throw new PluginDownloadException(PluginDownloadException.Verify, "The package is larger than allowed (100 MB).");

        byte[] bytes;
        try
        {
            using var response = await GetFollowingRedirectsAsync(url, cancellationToken);
            bytes = await ReadCappedAsync(response, version.Size, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new PluginDownloadException(PluginDownloadException.Failed, "Could not reach the download server.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new PluginDownloadException(PluginDownloadException.Failed, "The download timed out.", ex);
        }

        if (bytes.LongLength != version.Size)
            throw new PluginDownloadException(PluginDownloadException.Verify, "The download does not match the size the source reported.");

        var actualSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
        if (!string.Equals(actualSha256, version.Sha256.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new PluginDownloadException(PluginDownloadException.Verify, "The download does not match the checksum the source reported.");

        if (requireOfficialSignature && !PluginSigning.Verify(bytes, version.Signature))
            throw new PluginDownloadException(PluginDownloadException.Signature, "The package's signature could not be verified against the official key.");

        return bytes;
    }

    private async Task<HttpResponseMessage> GetFollowingRedirectsAsync(Uri start, CancellationToken cancellationToken)
    {
        var url = start;
        for (var hop = 0; hop <= MaxRedirects; hop++)
        {
            if (!PluginSourceUrls.IsAllowedUrl(url))
                throw new PluginDownloadException(PluginDownloadException.Refused, $"The download address is not on an allowed host: {url.Host}");

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.StatusCode is HttpStatusCode.MovedPermanently or HttpStatusCode.Found or HttpStatusCode.SeeOther
                or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect
                && response.Headers.Location is { } location)
            {
                response.Dispose();
                url = location.IsAbsoluteUri ? location : new Uri(url, location);
                continue;
            }

            try { response.EnsureSuccessStatusCode(); }
            catch { response.Dispose(); throw; }
            return response;
        }
        throw new PluginDownloadException(PluginDownloadException.Failed, "The download was redirected too many times.");
    }

    private static async Task<byte[]> ReadCappedAsync(HttpResponseMessage response, long expectedSize, CancellationToken cancellationToken)
    {
        var declared = response.Content.Headers.ContentLength;
        if (declared is { } announced && announced != expectedSize)
            throw new PluginDownloadException(PluginDownloadException.Verify, "The size of the download does not match the source.");

        var limit = Math.Max(expectedSize, 1);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > limit)
                throw new PluginDownloadException(PluginDownloadException.Verify, "The download is larger than the source says.");
        }
        return buffer.ToArray();
    }
}
