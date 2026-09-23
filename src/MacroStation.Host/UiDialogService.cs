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

    /// <param name="filter">A WinForms file filter, e.g. <c>"Profile (*.msprofile)|*.msprofile"</c>.</param>
    /// <returns>The chosen file's path and bytes, or (null, null) if the user canceled.</returns>
    Task<(string? Path, byte[]? Content)> OpenFileAsync(string title, string filter);

    /// <summary>Shows a native Save As dialog and writes <paramref name="content"/> to the chosen path.</summary>
    /// <returns>The chosen path, or null if the user canceled.</returns>
    Task<string?> SaveFileAsync(string title, string suggestedFileName, string filter, string defaultExtension, byte[] content);

    /// <returns>The chosen folder's path, or null if the user canceled.</returns>
    Task<string?> BrowseForFolderAsync(string title);
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

    public Task<(string? Path, byte[]? Content)> OpenFileAsync(string title, string filter)
    {
        var tcs = new TaskCompletionSource<(string?, byte[]?)>();
        ui.Post(_ =>
        {
            try
            {
                using var dialog = new OpenFileDialog
                {
                    Title = title,
                    Filter = filter + "|All files (*.*)|*.*",
                    CheckFileExists = true,
                };
                if (dialog.ShowDialog() != DialogResult.OK)
                {
                    tcs.SetResult((null, null));
                    return;
                }
                tcs.SetResult((dialog.FileName, File.ReadAllBytes(dialog.FileName)));
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }, null);
        return tcs.Task;
    }

    public Task<string?> SaveFileAsync(string title, string suggestedFileName, string filter, string defaultExtension, byte[] content)
    {
        var tcs = new TaskCompletionSource<string?>();
        ui.Post(_ =>
        {
            try
            {
                using var dialog = new SaveFileDialog
                {
                    Title = title,
                    Filter = filter + "|All files (*.*)|*.*",
                    FileName = suggestedFileName,
                    AddExtension = true,
                    DefaultExt = defaultExtension,
                };
                if (dialog.ShowDialog() != DialogResult.OK)
                {
                    tcs.SetResult(null);
                    return;
                }
                File.WriteAllBytes(dialog.FileName, content);
                tcs.SetResult(dialog.FileName);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }, null);
        return tcs.Task;
    }

    public Task<string?> BrowseForFolderAsync(string title)
    {
        var tcs = new TaskCompletionSource<string?>();
        ui.Post(_ =>
        {
            try
            {
                using var dialog = new FolderBrowserDialog
                {
                    Description = title,
                    UseDescriptionForTitle = true,
                };
                tcs.SetResult(dialog.ShowDialog() == DialogResult.OK ? dialog.SelectedPath : null);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }, null);
        return tcs.Task;
    }
}
