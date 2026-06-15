using System.Text.Json;
using PerMonitorVD.Utilities;

namespace PerMonitorVD.Configuration;

public sealed class ConfigEditorForm : Form
{
    private readonly ConfigStore _store;
    private readonly Action<AppConfig>? _onSaved;
    private readonly TextBox _editor;
    private readonly Label _status;

    public ConfigEditorForm(ConfigStore store, Action<AppConfig>? onSaved)
    {
        _store = store;
        _onSaved = onSaved;

        Text = "PerMonitorVD config.json";
        StartPosition = FormStartPosition.CenterScreen;
        Width = 920;
        Height = 720;
        MinimumSize = new Size(720, 520);

        _editor = new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            AcceptsReturn = true,
            AcceptsTab = true,
            Dock = DockStyle.Fill,
            Font = new Font(FontFamily.GenericMonospace, 10f),
        };

        _status = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Bottom,
            Height = 26,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 8, 0)
        };

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 44,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(6)
        };

        var closeButton = new Button { Text = "Close", Width = 90 };
        closeButton.Click += (_, _) => Close();

        var saveButton = new Button { Text = "Save + Reload", Width = 120 };
        saveButton.Click += (_, _) => SaveAndReload();

        var validateButton = new Button { Text = "Validate", Width = 90 };
        validateButton.Click += (_, _) => ValidateJson(showSuccess: true);

        var openFolderButton = new Button { Text = "Open folder", Width = 100 };
        openFolderButton.Click += (_, _) => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = AppPaths.BaseDirectory,
            UseShellExecute = true
        });

        var reloadButton = new Button { Text = "Reload file", Width = 100 };
        reloadButton.Click += (_, _) => LoadFromDisk();

        buttons.Controls.Add(closeButton);
        buttons.Controls.Add(saveButton);
        buttons.Controls.Add(validateButton);
        buttons.Controls.Add(openFolderButton);
        buttons.Controls.Add(reloadButton);

        Controls.Add(_editor);
        Controls.Add(buttons);
        Controls.Add(_status);

        LoadFromDisk();
    }

    private void LoadFromDisk()
    {
        try
        {
            AppPaths.EnsureDirectories();
            if (!File.Exists(AppPaths.ConfigPath))
                _store.Save(new AppConfig());

            _editor.Text = File.ReadAllText(AppPaths.ConfigPath);
            SetStatus($"Loaded {AppPaths.ConfigPath}");
        }
        catch (Exception ex)
        {
            SetStatus("Load failed: " + ex.Message);
        }
    }

    private AppConfig? ValidateJson(bool showSuccess)
    {
        try
        {
            var config = _store.Deserialize(_editor.Text);
            if (showSuccess)
                SetStatus("Valid config JSON.");
            return config;
        }
        catch (JsonException ex)
        {
            SetStatus($"JSON error at line {ex.LineNumber}, byte {ex.BytePositionInLine}: {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            SetStatus("Validation failed: " + ex.Message);
            return null;
        }
    }

    private void SaveAndReload()
    {
        var config = ValidateJson(showSuccess: false);
        if (config is null)
            return;

        try
        {
            _store.Save(config);
            _editor.Text = _store.Serialize(config);
            _onSaved?.Invoke(config);
            SetStatus("Saved and reloaded. Hotkeys were re-registered; run Repair after major workspace changes.");
        }
        catch (Exception ex)
        {
            SetStatus("Save failed: " + ex.Message);
        }
    }

    private void SetStatus(string text)
    {
        _status.Text = text;
        Log.Info("Config editor: " + text);
    }
}
