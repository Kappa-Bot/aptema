using System.ComponentModel;
using System.IO;
using System.Windows;
using LightPilot.App.ViewModels;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace LightPilot.App.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly MainWindowViewModel _viewModel;
    private readonly Window _window;
    private readonly Forms.NotifyIcon _notifyIcon;
    private bool _disposed;

    public TrayIconService(MainWindowViewModel viewModel, Window window)
    {
        _viewModel = viewModel;
        _window = window;

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = CreateTrayIcon(),
            Text = "Light Pilot",
            Visible = true,
            ContextMenuStrip = new Forms.ContextMenuStrip()
        };
        _notifyIcon.MouseClick += (_, args) =>
        {
            if (args.Button == Forms.MouseButtons.Left)
            {
                ShowWindow();
            }
        };
        _notifyIcon.DoubleClick += (_, _) => ShowWindow();

        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        RefreshMenuText();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }

    private static Drawing.Icon CreateTrayIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "LightPilot.ico");
        if (File.Exists(iconPath))
        {
            return new Drawing.Icon(iconPath);
        }

        if (!string.IsNullOrWhiteSpace(Environment.ProcessPath) && File.Exists(Environment.ProcessPath))
        {
            var associatedIcon = Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath);
            if (associatedIcon is not null)
            {
                return associatedIcon;
            }
        }

        return Drawing.SystemIcons.Application;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainWindowViewModel.AutoStatus)
            or nameof(MainWindowViewModel.CurrentModeText)
            or nameof(MainWindowViewModel.NextAdaptationText)
            or nameof(MainWindowViewModel.AutoEnabled))
        {
            RefreshMenuText();
        }
    }

    private void RefreshMenuText()
    {
        var state = new TrayMenuState(
            _viewModel.AutoEnabled,
            _viewModel.AutoStatus.StartsWith("Paused", StringComparison.OrdinalIgnoreCase),
            _viewModel.ComfortStateText,
            _viewModel.CurrentModeText,
            _viewModel.NextAdaptationText);

        _notifyIcon.ContextMenuStrip?.Items.Clear();
        foreach (var item in TrayMenuModelBuilder.Build(state))
        {
            if (item.IsSeparator)
            {
                _notifyIcon.ContextMenuStrip?.Items.Add(new Forms.ToolStripSeparator());
                continue;
            }

            var menuItem = new Forms.ToolStripMenuItem(item.Text)
            {
                Checked = item.IsChecked,
                Enabled = item.IsEnabled
            };
            menuItem.Click += (_, _) => Execute(item.CommandKey);
            _notifyIcon.ContextMenuStrip?.Items.Add(menuItem);
        }

        _notifyIcon.Text = TrayMenuModelBuilder.BuildTooltip(state);
    }

    private void Execute(TrayMenuCommandKey commandKey)
    {
        switch (commandKey)
        {
            case TrayMenuCommandKey.ToggleAuto:
                _viewModel.ToggleAutoCommand.Execute(null);
                break;
            case TrayMenuCommandKey.PauseThirtyMinutes:
                _viewModel.PauseThirtyMinutesCommand.Execute(null);
                break;
            case TrayMenuCommandKey.PauseUntilTomorrow:
                _viewModel.PauseUntilTomorrowCommand.Execute(null);
                break;
            case TrayMenuCommandKey.TooBright:
                _viewModel.TooBrightCommand.Execute(null);
                break;
            case TrayMenuCommandKey.TooDim:
                _viewModel.TooDimCommand.Execute(null);
                break;
            case TrayMenuCommandKey.Perfect:
                _viewModel.PerfectCommand.Execute(null);
                break;
            case TrayMenuCommandKey.Open:
                ShowWindow();
                break;
            case TrayMenuCommandKey.Settings:
                _viewModel.OpenSettingsCommand.Execute(null);
                ShowWindow();
                break;
            case TrayMenuCommandKey.Exit:
                _viewModel.ExitCommand.Execute(null);
                break;
        }
    }

    private void ShowWindow()
    {
        if (!_window.IsVisible)
        {
            _window.Show();
        }

        if (_window.WindowState == System.Windows.WindowState.Minimized)
        {
            _window.WindowState = System.Windows.WindowState.Normal;
        }

        _window.Activate();
    }
}
