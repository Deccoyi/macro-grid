namespace MacroStation.Host;

/// <summary>Opens native tool windows (Tercihler, Eklentiler, ...) — see <see cref="ToolWindow"/>. Marshaled
/// onto the WinForms UI thread the same way <see cref="UiDialogService"/> marshals native dialogs.</summary>
public interface IUiWindowService
{
    Task ShowToolWindowAsync(string kind, string title, string url, int width = 760, int height = 560);
}

public sealed class UiWindowService(SynchronizationContext ui) : IUiWindowService
{
    public Task ShowToolWindowAsync(string kind, string title, string url, int width = 760, int height = 560)
    {
        var tcs = new TaskCompletionSource();
        ui.Post(_ =>
        {
            try
            {
                ToolWindow.ShowOrFocus(kind, title, url, width, height);
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }, null);
        return tcs.Task;
    }
}
