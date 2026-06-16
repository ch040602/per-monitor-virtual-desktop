using PerMonitorVD.Configuration;
using PerMonitorVD.Core;
using PerMonitorVD.Utilities;

namespace PerMonitorVD.Overlay;

public sealed class HomeWindowForm : Form
{
    private const string WindowHwndDragFormat = "PerMonitorVD.WindowHwnd";

    private static readonly Color OverlayBack = Color.FromArgb(22, 24, 29);
    private static readonly Color PanelBack = Color.FromArgb(34, 38, 46);
    private static readonly Color WorkspaceBack = Color.FromArgb(48, 54, 64);
    private static readonly Color CurrentWorkspaceBack = Color.FromArgb(42, 92, 126);
    private static readonly Color DropWorkspaceBack = Color.FromArgb(68, 112, 92);
    private static readonly Color AppBack = Color.FromArgb(229, 234, 240);
    private static readonly Color AppText = Color.FromArgb(24, 28, 34);
    private static readonly Color TextMuted = Color.FromArgb(186, 195, 206);

    private readonly WorkspaceEngine _engine;
    private readonly AppConfig _config;
    private readonly ConfigStore _configStore;
    private readonly Label _summaryLabel = new();
    private readonly FlowLayoutPanel _overviewPanel = new();
    private readonly Button _refreshButton = new();
    private readonly Button _closeButton = new();
    private readonly ToolTip _toolTip = new() { ShowAlways = true };

    private HomeSnapshot? _snapshot;

    public HomeWindowForm(WorkspaceEngine engine, AppConfig config, ConfigStore configStore)
    {
        _engine = engine;
        _config = config;
        _configStore = configStore;

        Text = "PerMonitorVD Home";
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        Opacity = 0.97;
        BackColor = OverlayBack;
        ForeColor = Color.White;
        MinimumSize = new Size(760, 420);
        DoubleBuffered = true;

        BuildLayout();
        Resize += (_, _) => ReflowOverview();
        Shown += async (_, _) =>
        {
            PlaceAsOverlay();
            await RefreshSnapshotAsync();
        };
    }

    public async Task RefreshSnapshotAsync()
    {
        try
        {
            _snapshot = await _engine.GetHomeSnapshotAsync();
            BindOverview(_snapshot);
            BindSummary(_snapshot);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to refresh Home window.");
            MessageBox.Show(ex.Message, "PerMonitorVD Home", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void BuildLayout()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = OverlayBack,
            Padding = new Padding(12)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(layout);

        layout.Controls.Add(CreateHeader(), 0, 0);

        _overviewPanel.Dock = DockStyle.Fill;
        _overviewPanel.AutoScroll = true;
        _overviewPanel.WrapContents = true;
        _overviewPanel.FlowDirection = FlowDirection.LeftToRight;
        _overviewPanel.BackColor = OverlayBack;
        _overviewPanel.Padding = new Padding(0, 8, 0, 0);
        layout.Controls.Add(_overviewPanel, 0, 1);
    }

    private Control CreateHeader()
    {
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            BackColor = OverlayBack
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var titleLabel = new Label
        {
            Text = "PMVD Home",
            Font = new Font(Font.FontFamily, 18, FontStyle.Bold),
            AutoSize = true,
            Padding = new Padding(0, 7, 18, 0)
        };
        header.Controls.Add(titleLabel, 0, 0);

        _summaryLabel.AutoEllipsis = true;
        _summaryLabel.Dock = DockStyle.Fill;
        _summaryLabel.ForeColor = TextMuted;
        _summaryLabel.TextAlign = ContentAlignment.MiddleLeft;
        header.Controls.Add(_summaryLabel, 1, 0);

        StyleButton(_refreshButton, "Refresh");
        _refreshButton.Click += async (_, _) => await RefreshSnapshotAsync();
        header.Controls.Add(_refreshButton, 2, 0);

        StyleButton(_closeButton, "Close");
        _closeButton.Click += (_, _) => Hide();
        header.Controls.Add(_closeButton, 3, 0);

        return header;
    }

    private void PlaceAsOverlay()
    {
        var workingArea = Screen.FromPoint(Cursor.Position).WorkingArea;
        var width = Math.Max(MinimumSize.Width, (int)(workingArea.Width * 0.90));
        var height = Math.Max(MinimumSize.Height, (int)(workingArea.Height * 0.86));
        width = Math.Min(width, workingArea.Width - 36);
        height = Math.Min(height, workingArea.Height - 36);

        Bounds = new Rectangle(
            workingArea.Left + (workingArea.Width - width) / 2,
            workingArea.Top + (workingArea.Height - height) / 2,
            width,
            height);
    }

    private void BindSummary(HomeSnapshot snapshot)
    {
        var current = snapshot.Workspaces
            .Where(w => w.IsCurrent)
            .Select(w => $"{w.MonitorName}: Desktop {w.WorkspaceLabel}");

        _summaryLabel.Text = string.Join("   |   ", current);
    }

    private void BindOverview(HomeSnapshot snapshot)
    {
        _overviewPanel.SuspendLayout();
        _overviewPanel.Controls.Clear();

        foreach (var monitor in snapshot.Monitors)
            _overviewPanel.Controls.Add(CreateMonitorLane(monitor, snapshot));

        _overviewPanel.ResumeLayout();
    }

    private Panel CreateMonitorLane(MonitorHomeItem monitor, HomeSnapshot snapshot)
    {
        var lane = new Panel
        {
            Width = CalculateLaneWidth(snapshot.Monitors.Count),
            Height = CalculateLaneHeight(snapshot.Monitors.Count),
            BackColor = PanelBack,
            Margin = new Padding(0, 0, 12, 12),
            Padding = new Padding(10)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = PanelBack
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        lane.Controls.Add(layout);

        layout.Controls.Add(CreateMonitorHeader(monitor), 0, 0);

        var cards = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            BackColor = PanelBack,
            Padding = new Padding(0, 8, 0, 0)
        };
        layout.Controls.Add(cards, 0, 1);

        var workspaces = snapshot.Workspaces
            .Where(w => string.Equals(w.MonitorKey, monitor.MonitorKey, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        foreach (var workspace in workspaces)
            cards.Controls.Add(CreateWorkspaceCard(workspace, snapshot, lane.Width, workspaces.Length));

        return lane;
    }

    private Control CreateMonitorHeader(MonitorHomeItem monitor)
    {
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            BackColor = PanelBack
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var title = new Label
        {
            Text = $"{monitor.MonitorName}  {FormatLimit(monitor)}",
            ForeColor = Color.White,
            Font = new Font(Font.FontFamily, 10, FontStyle.Bold),
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft
        };
        header.Controls.Add(title, 0, 0);

        var maxLabel = new Label
        {
            Text = "Max",
            ForeColor = TextMuted,
            AutoSize = true,
            Padding = new Padding(0, 9, 6, 0)
        };
        header.Controls.Add(maxLabel, 1, 0);

        var numeric = new NumericUpDown
        {
            Minimum = 0,
            Maximum = 512,
            Width = 66,
            Value = Math.Clamp(monitor.MaxManagedWindows, 0, 512),
            Tag = monitor.MonitorKey,
            BorderStyle = BorderStyle.FixedSingle
        };
        numeric.ValueChanged += (_, _) =>
        {
            if (numeric.Tag is not string monitorKey)
                return;

            _config.SetMaxManagedWindows(monitorKey, (int)numeric.Value);
            _configStore.Save(_config);
            if (_snapshot is not null)
                BindOverview(_snapshot);
        };
        header.Controls.Add(numeric, 2, 0);

        return header;
    }

    private Panel CreateWorkspaceCard(WorkspaceHomeItem workspace, HomeSnapshot snapshot, int laneWidth, int workspaceCount)
    {
        var columns = laneWidth >= 880 ? Math.Min(3, workspaceCount) : laneWidth >= 560 ? Math.Min(2, workspaceCount) : 1;
        var cardWidth = Math.Max(180, ((laneWidth - 24) / Math.Max(1, columns)) - 10);
        var windows = snapshot.Windows
            .Where(w => !w.Ignored && string.Equals(w.WorkspaceId, workspace.WorkspaceId, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var card = new Panel
        {
            Width = cardWidth,
            Height = Math.Max(160, Math.Min(330, CalculateLaneHeight(snapshot.Monitors.Count) - 72)),
            Margin = new Padding(0, 0, 10, 10),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = workspace.IsCurrent ? CurrentWorkspaceBack : WorkspaceBack,
            Tag = workspace.WorkspaceId,
            AllowDrop = true
        };
        WireDropTarget(card, workspace);

        var title = new Label
        {
            Text = $"Desktop {workspace.WorkspaceLabel}",
            Font = new Font(Font.FontFamily, 11, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = false,
            Size = new Size(cardWidth - 18, 26),
            Location = new Point(9, 8),
            AutoEllipsis = true
        };
        card.Controls.Add(title);

        var apps = new FlowLayoutPanel
        {
            Location = new Point(9, 42),
            Size = new Size(cardWidth - 18, card.Height - 50),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            AutoScroll = true,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            BackColor = card.BackColor,
            AllowDrop = true
        };
        WireDropTarget(apps, workspace);
        card.Controls.Add(apps);

        foreach (var window in windows)
            apps.Controls.Add(CreateAppChip(window, workspace, Math.Max(128, Math.Min(220, cardWidth - 32))));

        if (windows.Length == 0)
        {
            apps.Controls.Add(new Label
            {
                Text = "Empty",
                ForeColor = TextMuted,
                AutoSize = true,
                Padding = new Padding(2, 5, 0, 0)
            });
        }

        return card;
    }

    private Label CreateAppChip(WindowHomeItem window, WorkspaceHomeItem workspace, int width)
    {
        var chip = new Label
        {
            Text = FormatAppName(window),
            ForeColor = AppText,
            BackColor = AppBack,
            AutoEllipsis = true,
            AutoSize = false,
            Size = new Size(width, 30),
            Margin = new Padding(0, 0, 8, 8),
            Padding = new Padding(8, 6, 8, 0),
            Cursor = Cursors.Hand,
            Tag = window.Hwnd,
            AllowDrop = true
        };
        WireDropTarget(chip, workspace);

        _toolTip.SetToolTip(chip, string.IsNullOrWhiteSpace(window.Title)
            ? window.ProcessName
            : $"{window.ProcessName}\n{window.Title}");

        chip.MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Left || chip.Tag is not string hwnd)
                return;

            var data = new DataObject();
            data.SetData(WindowHwndDragFormat, hwnd);
            chip.DoDragDrop(data, DragDropEffects.Move);
        };

        return chip;
    }

    private void WireDropTarget(Control target, WorkspaceHomeItem workspace)
    {
        var originalBack = target.BackColor;

        target.DragEnter += (_, e) =>
        {
            if (!HasWindowDragData(e))
                return;

            e.Effect = DragDropEffects.Move;
            target.BackColor = DropWorkspaceBack;
        };

        target.DragLeave += (_, _) => target.BackColor = originalBack;

        target.DragDrop += async (_, e) =>
        {
            target.BackColor = originalBack;
            var hwnd = e.Data?.GetData(WindowHwndDragFormat) as string;
            if (string.IsNullOrWhiteSpace(hwnd))
                return;

            await _engine.MoveWindowToWorkspaceAsync(hwnd, workspace.WorkspaceId);
            await RefreshSnapshotAsync();
        };
    }

    private void ReflowOverview()
    {
        if (_snapshot is null)
            return;

        BindOverview(_snapshot);
    }

    private int CalculateLaneWidth(int monitorCount)
    {
        var available = Math.Max(1, _overviewPanel.ClientSize.Width - 24);
        var columns = available >= 1600 ? Math.Min(3, monitorCount) : available >= 980 ? Math.Min(2, monitorCount) : 1;
        return Math.Max(320, (available / Math.Max(1, columns)) - 14);
    }

    private int CalculateLaneHeight(int monitorCount)
    {
        var available = Math.Max(1, _overviewPanel.ClientSize.Height - 22);
        var columns = _overviewPanel.ClientSize.Width >= 980 ? Math.Min(2, Math.Max(1, monitorCount)) : 1;
        var rows = (int)Math.Ceiling(monitorCount / (double)Math.Max(1, columns));
        return Math.Max(230, (available / Math.Max(1, rows)) - 14);
    }

    private static bool HasWindowDragData(DragEventArgs e)
    {
        return e.Data?.GetDataPresent(WindowHwndDragFormat) == true;
    }

    private static void StyleButton(Button button, string text)
    {
        button.Text = text;
        button.AutoSize = true;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = Color.FromArgb(90, 100, 118);
        button.BackColor = Color.FromArgb(48, 54, 66);
        button.ForeColor = Color.White;
        button.Margin = new Padding(6, 4, 0, 4);
        button.Padding = new Padding(10, 4, 10, 4);
    }

    private static string FormatLimit(MonitorHomeItem monitor)
    {
        return monitor.MaxManagedWindows <= 0
            ? $"{monitor.WindowCount}/all"
            : $"{monitor.WindowCount}/{monitor.MaxManagedWindows}";
    }

    private static string FormatAppName(WindowHomeItem window)
    {
        return window.ProcessName;
    }
}
