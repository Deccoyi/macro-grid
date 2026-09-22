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
}
