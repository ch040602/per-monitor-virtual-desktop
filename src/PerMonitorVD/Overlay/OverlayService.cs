namespace PerMonitorVD.Overlay;

public sealed class OverlayService
{
    private readonly SynchronizationContext? _syncContext;
    private readonly Dictionary<string, OverlayForm> _forms = new(StringComparer.OrdinalIgnoreCase);

    public OverlayService(SynchronizationContext? syncContext)
    {
        _syncContext = syncContext;
    }

    public void ShowWorkspace(string monitorKey, string label, bool enabled)
    {
        if (!enabled) return;
        ShowOnUiThread(() =>
        {
            var screen = Screen.AllScreens.FirstOrDefault(s => Normalize(s.DeviceName) == monitorKey) ?? Screen.PrimaryScreen;
            ShowOnScreen(screen, label);
        });
    }

    public void ShowText(string text, bool enabled)
    {
        if (!enabled) return;
        ShowOnUiThread(() => ShowOnScreen(Screen.PrimaryScreen, text));
    }

    private void ShowOnScreen(Screen? screen, string text)
    {
        if (screen is null) return;

        var key = Normalize(screen.DeviceName);
        if (!_forms.TryGetValue(key, out var form) || form.IsDisposed)
        {
            form = new OverlayForm();
            _forms[key] = form;
        }

        form.ShowText(screen.WorkingArea, text);
    }

    private void ShowOnUiThread(Action action)
    {
        if (_syncContext is null)
        {
            action();
            return;
        }

        _syncContext.Post(_ => action(), null);
    }

    private static string Normalize(string value) => value.Replace("\\\\.\\", "", StringComparison.OrdinalIgnoreCase).Trim();
}

internal sealed class OverlayForm : Form
{
    private readonly Label _label;
    private readonly System.Windows.Forms.Timer _timer;

    public OverlayForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.Black;
        Opacity = 0.78;

        _label = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = Color.White,
            BackColor = Color.Transparent,
            Font = new Font(FontFamily.GenericSansSerif, 42, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter
        };
        Controls.Add(_label);

        _timer = new System.Windows.Forms.Timer { Interval = 700 };
        _timer.Tick += (_, _) => Hide();
    }

    public void ShowText(Rectangle workingArea, string text)
    {
        _label.Text = text;
        Size = new Size(260, 140);
        Location = new Point(
            workingArea.Left + (workingArea.Width - Width) / 2,
            workingArea.Top + (workingArea.Height - Height) / 2);

        _timer.Stop();
        Show();
        BringToFront();
        _timer.Start();
    }
}
