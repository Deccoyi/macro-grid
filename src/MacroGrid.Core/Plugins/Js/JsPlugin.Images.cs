using System.Text.Json;
using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Core.Plugins.Js;

public sealed partial class JsPlugin : IPluginImageSource
{
    /// <summary>Passes the host's request to the function the script gave to <c>host.images.onRequest</c>. Runs on the plugin thread; never waits for the script.</summary>
    public void RequestImages(IReadOnlyList<string> imageIds)
    {
        var json = JsonSerializer.Serialize(imageIds);
        _ = Post(() => { Invoke("__imagesRequest", json); return 0; }).ContinueWith(
            t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
    }

    /// <summary><c>host.images.put</c>: one picture for an id the host asked for. Returns whether it was accepted.</summary>
    private bool ImagesPut(string imageId, string dataUrl) => _host!.Images.Put(imageId, dataUrl);
}
