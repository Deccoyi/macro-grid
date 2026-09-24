using Microsoft.Web.WebView2.Core;

namespace MacroGrid.Host;

/// <summary>
/// The one WebView2 environment shared by the editor and every tool window. Without an explicit user data folder WebView2
/// writes next to the exe (<c>MacroGrid.exe.WebView2</c>), which is under Program Files after an install and not writable
/// for a normal user: the window then fails with E_ACCESSDENIED (0x80070005). The folder lives in the user's local
/// application data instead.
/// </summary>
internal static class WebViewEnvironment
{
    private static Task<CoreWebView2Environment>? _environment;

    public static string UserDataFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MacroGrid", "WebView2");

    /// <summary>Creates the environment on first use. Call it from the UI thread.</summary>
    public static Task<CoreWebView2Environment> GetAsync()
    {
        if (_environment is { IsFaulted: true }) _environment = null; // let the next window try again
        return _environment ??= CoreWebView2Environment.CreateAsync(browserExecutableFolder: null, userDataFolder: UserDataFolder);
    }
}
