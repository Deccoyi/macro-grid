namespace MacroGrid.Host.Ui;

/// <summary>The user agreement with Accept and Decline, shown by <see cref="AgreementGate"/> before the server starts. OK means accepted.</summary>
internal sealed class AgreementDialog : Form
{
    public AgreementDialog(string agreementText)
    {
        Text = HostText.Get("agreement.title");
        Width = 760;
        Height = 620;
        MinimumSize = new Size(520, 400);
        StartPosition = FormStartPosition.CenterScreen;
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        MaximizeBox = false;
        ShowInTaskbar = true;
        TopMost = true;

        var intro = new Label
        {
            Text = HostText.Get("agreement.intro"),
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = 44,
            Padding = new Padding(12, 12, 12, 0),
        };

        // The agreement ships with Windows line ends; a read-only text box shows it as written and lets the person scroll and select.
        var text = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 9.5f),
            Text = agreementText,
            BackColor = SystemColors.Window,
        };
        text.Select(0, 0);

        var accept = new Button { Text = HostText.Get("agreement.accept"), DialogResult = DialogResult.OK, AutoSize = true, Padding = new Padding(10, 4, 10, 4) };
        var decline = new Button { Text = HostText.Get("agreement.decline"), DialogResult = DialogResult.Cancel, AutoSize = true, Padding = new Padding(10, 4, 10, 4) };
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 52,
            Padding = new Padding(12, 10, 12, 10),
        };
        buttons.Controls.Add(accept);
        buttons.Controls.Add(decline);

        Controls.Add(text);
        Controls.Add(buttons);
        Controls.Add(intro);
        CancelButton = decline;
    }
}
