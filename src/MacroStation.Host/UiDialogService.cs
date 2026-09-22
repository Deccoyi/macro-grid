namespace MacroStation.Host;

/// <summary>
/// Shows native Windows dialogs from an ASP.NET Core request handler. Common dialogs (OpenFileDialog)
/// are STA/UI-thread-only, but API requests run on Kestrel's thread pool — so every call is marshaled
/// onto the WinForms UI thread via the SynchronizationContext captured at startup.
/// </summary>
public interface IUiDialogService
{
    /// <returns>The chosen path, or null if the user canceled.</returns>
    Task<string?> BrowseForExecutableAsync();

    /// <returns>The chosen file's path and contents, or (null, null) if the user canceled.</returns>
    Task<(string? Path, string? Content)> OpenJsonFileAsync(string title);

    /// <summary>Shows a native Save As dialog and writes <paramref name="content"/> to the chosen path.</summary>
    /// <returns>The chosen path, or null if the user canceled.</returns>
    Task<string?> SaveJsonFileAsync(string title, string suggestedFileName, string content);
}

public sealed class UiDialogService(SynchronizationContext ui) : IUiDialogService
{
    public Task<string?> BrowseForExecutableAsync()
    {
        var tcs = new TaskCompletionSource<string?>();
        ui.Post(_ =>
        {
            try
            {
                using var dialog = new OpenFileDialog
                {
                    Title = "Uygulama seç",
                    Filter = "Uygulamalar (*.exe)|*.exe|Tüm dosyalar (*.*)|*.*",
                    CheckFileExists = true,
                };
                tcs.SetResult(dialog.ShowDialog() == DialogResult.OK ? dialog.FileName : null);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }, null);
        return tcs.Task;
    }

    public Task<(string? Path, string? Content)> OpenJsonFileAsync(string title)
    {
        var tcs = new TaskCompletionSource<(string?, string?)>();
        ui.Post(_ =>
        {
            try
            {
                using var dialog = new OpenFileDialog
                {
                    Title = title,
                    Filter = "JSON (*.json)|*.json|Tüm dosyalar (*.*)|*.*",
                    CheckFileExists = true,
                };
                if (dialog.ShowDialog() != DialogResult.OK)
                {
                    tcs.SetResult((null, null));
                    return;
                }
                tcs.SetResult((dialog.FileName, File.ReadAllText(dialog.FileName)));
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }, null);
        return tcs.Task;
    }

    public Task<string?> SaveJsonFileAsync(string title, string suggestedFileName, string content)
    {
        var tcs = new TaskCompletionSource<string?>();
        ui.Post(_ =>
        {
            try
            {
                using var dialog = new SaveFileDialog
                {
                    Title = title,
                    Filter = "JSON (*.json)|*.json|Tüm dosyalar (*.*)|*.*",
                    FileName = suggestedFileName,
                    AddExtension = true,
                    DefaultExt = "json",
                };
                if (dialog.ShowDialog() != DialogResult.OK)
                {
                    tcs.SetResult(null);
                    return;
                }
                File.WriteAllText(dialog.FileName, content);
                tcs.SetResult(dialog.FileName);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }, null);
        return tcs.Task;
    }
}
