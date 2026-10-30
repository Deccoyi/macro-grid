using System.Runtime.InteropServices;
using System.Text;

namespace MacroGrid.Host.Ui;

/// <summary>A crash report waiting to be shown, and the marker files it was built from (deleted right before the window shows).</summary>
internal sealed record PendingCrashReport(string Text, IReadOnlyList<string> Files);

/// <summary>
/// The crash report window: the exact report text and Save to a file, Copy and Close. It sends nothing and opens no network connection; what
/// is saved or copied is exactly the text in the box. The report itself is English; only the window around it follows the language.
/// </summary>
internal sealed class CrashReportDialog : Form
{
    private readonly TextBox _report;
    private readonly Label _status;

    public CrashReportDialog(string reportText)
    {
        Text = HostText.Get("crash.title");
        Width = 820;
        Height = 640;
        MinimumSize = new Size(560, 420);
        StartPosition = FormStartPosition.CenterScreen;
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        MaximizeBox = false;
        ShowInTaskbar = true;
        TopMost = true;

        var intro = new Label
        {
            Text = HostText.Get("crash.intro"),
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = 62,
            Padding = new Padding(12, 12, 12, 0),
        };

        // The text is set as it is (never through HostText.Get, which would format it); a read-only box lets the person scroll and select.
        _report = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            MaxLength = int.MaxValue,
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 9.5f),
            Text = reportText,
            BackColor = SystemColors.Window,
        };
        _report.Select(0, 0);

        var close = new Button { Text = HostText.Get("crash.close"), DialogResult = DialogResult.Cancel, AutoSize = true, Padding = new Padding(10, 4, 10, 4) };
        var copy = new Button { Text = HostText.Get("crash.copy"), AutoSize = true, Padding = new Padding(10, 4, 10, 4) };
        var save = new Button { Text = HostText.Get("crash.save"), AutoSize = true, Padding = new Padding(10, 4, 10, 4) };
        // DialogResult only closes a window shown with ShowDialog; the tray shows this one with Show, so Close closes it itself.
        close.Click += (_, _) => Close();
        copy.Click += (_, _) => Copy();
        save.Click += (_, _) => Save();
        _status = new Label { AutoSize = true, Margin = new Padding(12, 8, 0, 0) };

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 52,
            Padding = new Padding(12, 10, 12, 10),
        };
        buttons.Controls.Add(close);
        buttons.Controls.Add(copy);
        buttons.Controls.Add(save);
        buttons.Controls.Add(_status);

        Controls.Add(_report);
        Controls.Add(buttons);
        Controls.Add(intro);
        CancelButton = close;
    }

    private void Save()
    {
        using var dialog = new SaveFileDialog
        {
            FileName = $"macro-grid-crash-{DateTime.Now:yyyyMMdd-HHmm}.txt",
            Filter = HostText.Get("crash.fileFilter"),
            DefaultExt = "txt",
            OverwritePrompt = true,
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            File.WriteAllText(dialog.FileName, _report.Text, new UTF8Encoding(false));
            _status.Text = HostText.Get("crash.saved", dialog.FileName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _status.Text = HostText.Get("crash.saveFailed", ex.Message);
        }
    }

    private void Copy()
    {
        try
        {
            Clipboard.SetText(_report.Text);
            _status.Text = HostText.Get("crash.copied");
        }
        catch (ExternalException)
        {
            // Another app holds the clipboard.
            _status.Text = HostText.Get("crash.copyFailed");
        }
    }
}
