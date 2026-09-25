namespace MacroGrid.Core.Updates;

/// <summary>Why an installer download did not produce a usable file. <see cref="Code"/> is stable ("refused", "failed", "verify") so the editor can show a translated message.</summary>
public sealed class UpdateDownloadException(string code, string message, Exception? inner = null) : Exception(message, inner)
{
    /// <summary>The release cannot be installed from here (no installer, no digest, or a URL outside the allow-list).</summary>
    public const string Refused = "refused";

    /// <summary>The network or the disk failed.</summary>
    public const string Failed = "failed";

    /// <summary>The file is not the one the release describes: wrong size or wrong SHA-256. It has been deleted.</summary>
    public const string Verify = "verify";

    public string Code { get; } = code;
}

/// <summary>
/// Downloads the installer of a release into <c>&lt;updatesRoot&gt;\&lt;version&gt;\</c> and checks it against the digest GitHub reports.
/// Every URL, including each redirect target, must pass <see cref="ReleaseFeed.IsAllowedUrl"/>; the <see cref="HttpClient"/> handed in must
/// have automatic redirects switched off so this class can look at each hop. The file appears under its final name only after it verified.
/// </summary>
public sealed class InstallerDownloader(HttpClient http)
{
    private const int MaxRedirects = 5;
    private const long MaxUnknownSize = 500L * 1024 * 1024;

    /// <param name="progress">Fraction from 0 to 1 while downloading (only when the size is known).</param>
    /// <returns>The path of the verified installer.</returns>
    public async Task<string> DownloadAsync(ReleaseInfo release, string updatesRoot, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(release);
        if (release.Installer is not { Sha256: { } digest } asset)
            throw new UpdateDownloadException(UpdateDownloadException.Refused, "The release has no installer with a digest.");

        var directory = Path.Combine(updatesRoot, release.Version.ToString());
        var target = Path.Combine(directory, Path.GetFileName(asset.Name));
        var part = target + ".part";

        try
        {
            Directory.CreateDirectory(directory);
            if (File.Exists(target) && await DownloadVerifier.MatchesSha256Async(target, digest, cancellationToken))
            {
                progress?.Report(1);
                return target;
            }

            using (var response = await GetFollowingRedirectsAsync(asset.DownloadUrl, cancellationToken))
                await SaveAsync(response, part, asset, progress, cancellationToken);

            if (!await DownloadVerifier.MatchesSha256Async(part, digest, cancellationToken))
                throw new UpdateDownloadException(UpdateDownloadException.Verify, "The downloaded file does not match the digest of the release.");

            File.Move(part, target, overwrite: true);
            return target;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException)
        {
            DeleteQuietly(part);
            throw new UpdateDownloadException(UpdateDownloadException.Failed, ex.Message, ex);
        }
        catch
        {
            DeleteQuietly(part);
            throw;
        }
    }

    private async Task<HttpResponseMessage> GetFollowingRedirectsAsync(Uri start, CancellationToken cancellationToken)
    {
        var url = start;
        for (var hop = 0; hop <= MaxRedirects; hop++)
        {
            if (!ReleaseFeed.IsAllowedUrl(url))
                throw new UpdateDownloadException(UpdateDownloadException.Refused, $"The download address is not on an allowed host: {url.Host}");

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.StatusCode is System.Net.HttpStatusCode.MovedPermanently or System.Net.HttpStatusCode.Found
                or System.Net.HttpStatusCode.SeeOther or System.Net.HttpStatusCode.TemporaryRedirect or System.Net.HttpStatusCode.PermanentRedirect
                && response.Headers.Location is { } location)
            {
                response.Dispose();
                url = location.IsAbsoluteUri ? location : new Uri(url, location);
                continue;
            }

            try
            {
                response.EnsureSuccessStatusCode();
            }
            catch
            {
                response.Dispose();
                throw;
            }
            return response;
        }
        throw new UpdateDownloadException(UpdateDownloadException.Failed, "The download was redirected too many times.");
    }

    private static async Task SaveAsync(HttpResponseMessage response, string path, ReleaseAsset asset, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var expected = asset.Size > 0 ? asset.Size : (long?)null;
        var total = response.Content.Headers.ContentLength ?? expected;
        if (expected is { } declared && total is { } announced && announced != declared)
            throw new UpdateDownloadException(UpdateDownloadException.Verify, "The size of the download does not match the release.");

        var limit = expected ?? MaxUnknownSize;
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var destination = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);

        var buffer = new byte[81920];
        long written = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            written += read;
            if (written > limit)
                throw new UpdateDownloadException(UpdateDownloadException.Verify, "The download is larger than the release says.");
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            if (total is > 0) progress?.Report(Math.Min(1.0, (double)written / total.Value));
        }

        if (expected is { } size && written != size)
            throw new UpdateDownloadException(UpdateDownloadException.Verify, "The download is smaller than the release says.");
    }

    private static void DeleteQuietly(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // A locked or missing partial file is not worth failing over.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
