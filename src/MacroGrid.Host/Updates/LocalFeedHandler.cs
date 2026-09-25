using System.Net;
using System.Text;

namespace MacroGrid.Host.Updates;

/// <summary>
/// Debug aid: answers every request with the contents of a local JSON file instead of calling GitHub, so the update flow can be tried without
/// publishing a release. Switched on by the <c>MACROGRID_UPDATE_FEED_FILE</c> environment variable (see <see cref="Path"/>); never used otherwise.
/// </summary>
internal sealed class LocalFeedHandler(string path) : HttpMessageHandler
{
    public const string EnvironmentVariable = "MACROGRID_UPDATE_FEED_FILE";

    public string Path { get; } = path;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var json = await File.ReadAllTextAsync(Path, cancellationToken);
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }
}
