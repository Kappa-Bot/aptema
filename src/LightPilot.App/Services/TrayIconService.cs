using System.ComponentModel;
using System.IO;
using System.Windows;
using LightPilot.App.Presentation;
using LightPilot.App.ViewModels;
using LightPilot.Core;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace LightPilot.App.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly MainWindowViewModel _viewModel;
    private readonly Window _window;
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Dictionary<TrayIconState, Drawing.Icon> _icons;
    private readonly TrayCommandRouter _router;
    private readonly TrayFlyoutWindow _flyout;
    private readonly OsdWindow _osd = new();
    private readonly HotkeyHost _hotkeyHost;
    private readonly TrayNotificationTracker _notificationTracker = new();
    private bool _disposed;

    public TrayIconService(MainWindowViewModel viewModel, Window window)
    {
        _viewModel = viewModel;
        _window = window;
        _hotkeyHost = new HotkeyHost(viewModel.Settings.Hotkeys);
        _icons = LoadIcons();
        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = _icons[TrayIconState.Active],
            Text = "Aptema",
            Visible = true,
            ContextMenuStrip = new Forms.ContextMenuStrip()
        };
        _router = new TrayCommandRouter(new CommandTarget(this));
        _flyout = new TrayFlyoutWindow(viewModel, _router, size => NativeWindowPlacement.PlaceFlyout(_notifyIcon, size));
        _notifyIcon.MouseClick += NotifyIcon_MouseClick;
        _notifyIcon.DoubleClick += (_, _) => ShowWindow();
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        _viewModel.RequestQuickAdjust += ViewModel_RequestQuickAdjust;
        _viewModel.FeedbackApplied += ViewModel_FeedbackApplied;
        _hotkeyHost.Invoked += HotkeyHost_Invoked;
        _viewModel.SetQuickAdjustShortcutAvailability(_hotkeyHost.Registration.Status != LightPilot.Application.OperationStatus.Conflict);
        RefreshPresentation(notifySignificantChange: false);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        _viewModel.RequestQuickAdjust -= ViewModel_RequestQuickAdjust;
        _viewModel.FeedbackApplied -= ViewModel_FeedbackApplied;
        _hotkeyHost.Invoked -= HotkeyHost_Invoked;
        _hotkeyHost.Dispose();
        _flyout.Close();
        _osd.Close();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        foreach (var icon in _icons.Values.Distinct())
        {
            icon.Dispose();
        }
    }

    private void NotifyIcon_MouseClick(object? sender, Forms.MouseEventArgs args)
    {
        if (args.Button == Forms.MouseButtons.Left)
        {
            _flyout.ToggleNearTaskbar();
        }
    }

    private void ViewModel_RequestQuickAdjust(object? sender, EventArgs e) => _flyout.ToggleNearTaskbar();

    private void HotkeyHost_Invoked(HotkeyAction action)
    {
        if (action == HotkeyAction.QuickAdjust)
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke(_flyout.ToggleNearTaskbar);
        }
    }

    private void ViewModel_FeedbackApplied(object? sender, FeedbackPresentationEventArgs e)
    {
        _osd.ShowFeedback(e.Message, e.CanUndo, () => _viewModel.UndoFeedbackCommand.Execute(null));
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainWindowViewModel.AutoStatus)
            or nameof(MainWindowViewModel.CurrentModeText)
            or nameof(MainWindowViewModel.NextAdaptationText)
            or nameof(MainWindowViewModel.AutoEnabled)
            or nameof(MainWindowViewModel.RuntimeSnapshot)
            or nameof(MainWindowViewModel.ComfortStateText))
        {
            RefreshPresentation(notifySignificantChange: true);
        }
    }

    private void RefreshPresentation(bool notifySignificantChange)
    {
        var state = BuildState();
        var iconState = ResolveIconState();
        var identity = BuildNotificationIdentity(iconState);
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
            menuItem.Click += (_, _) => _router.Execute(item.CommandKey);
            _notifyIcon.ContextMenuStrip?.Items.Add(menuItem);
        }

        _notifyIcon.Text = TrayMenuModelBuilder.BuildTooltip(state);
        _notifyIcon.Icon = _icons[iconState];
        var now = DateTimeOffset.UtcNow;
        if (notifySignificantChange && _notificationTracker.ShouldNotify(identity, now))
        {
            NotifyStateChange(iconState);
        }
    }

    private TrayMenuState BuildState() => new(
        _viewModel.AutoEnabled,
        !_viewModel.AutoEnabled || _viewModel.RuntimeSnapshot.PausedUntil is not null,
        _viewModel.ComfortStateText,
        _viewModel.CurrentModeText,
        _viewModel.NextAdaptationText,
        _viewModel.ShortcutAvailable);

    private TrayNotificationIdentity BuildNotificationIdentity(TrayIconState state)
    {
        var issues = string.Join('|', _viewModel.RuntimeSnapshot.Health.Issues.Order(StringComparer.OrdinalIgnoreCase));
        return new TrayNotificationIdentity(state, issues);
    }

    private TrayIconState ResolveIconState()
    {
        var displays = _viewModel.RuntimeSnapshot.Displays;
        var failed = displays.Any(item => item.ApplyResult.State == MonitorControlState.Failed);
        var degraded = _viewModel.RuntimeSnapshot.Health.IsDegraded
            || displays.Any(item => item.ApplyResult.State is MonitorControlState.Degraded or MonitorControlState.FallbackUsed);
        return TrayPresentation.ResolveIconState(
            !_viewModel.AutoEnabled || _viewModel.RuntimeSnapshot.PausedUntil is not null,
            degraded,
            failed);
    }

    private void NotifyStateChange(TrayIconState state)
    {
        var text = state switch
        {
            TrayIconState.Paused => "Comfort paused",
            TrayIconState.Degraded => "A display is using a safe fallback",
            TrayIconState.Error => "Aptema needs attention",
            _ => null
        };
        if (text is null)
        {
            return;
        }

        _notifyIcon.BalloonTipTitle = "Aptema";
        _notifyIcon.BalloonTipText = text;
        _notifyIcon.ShowBalloonTip(2500);
    }

    private void ShowWindow()
    {
        _flyout.Hide();
        if (!_window.IsVisible)
        {
            _window.Show();
        }

        if (_window.WindowState == WindowState.Minimized)
        {
            _window.WindowState = WindowState.Normal;
        }

        _window.Activate();
    }

    private static Dictionary<TrayIconState, Drawing.Icon> LoadIcons()
    {
        var active = LoadIcon("Assets/Aptema.ico");
        return new Dictionary<TrayIconState, Drawing.Icon>
        {
            [TrayIconState.Active] = active,
            [TrayIconState.Paused] = LoadIcon("Assets/Aptema-paused.ico", active),
            [TrayIconState.Degraded] = LoadIcon("Assets/Aptema-degraded.ico", active),
            [TrayIconState.Error] = LoadIcon("Assets/Aptema-error.ico", active)
        };
    }

    private static Drawing.Icon LoadIcon(string resourcePath, Drawing.Icon? fallback = null)
    {
        var resource = System.Windows.Application.GetResourceStream(new Uri(resourcePath, UriKind.Relative));
        if (resource?.Stream is not null)
        {
            using var icon = new Drawing.Icon(resource.Stream);
            return (Drawing.Icon)icon.Clone();
        }

        if (fallback is not null)
        {
            return (Drawing.Icon)fallback.Clone();
        }

        if (!string.IsNullOrWhiteSpace(Environment.ProcessPath) && File.Exists(Environment.ProcessPath)
            && Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath) is { } associated)
        {
            return associated;
        }

        return (Drawing.Icon)Drawing.SystemIcons.Application.Clone();
    }

    private sealed class CommandTarget(TrayIconService owner) : ITrayCommandTarget
    {
        public void ToggleAuto() => owner._viewModel.ToggleAutoCommand.Execute(null);
        public void PauseOrResume() => owner._viewModel.PauseResumeCommand.Execute(null);
        public void PauseUntilTomorrow() => owner._viewModel.PauseUntilTomorrowCommand.Execute(null);
        public void TooBright() => owner._viewModel.TooBrightCommand.Execute(null);
        public void TooDim() => owner._viewModel.TooDimCommand.Execute(null);
        public void Warmer() => owner._viewModel.TooColdCommand.Execute(null);
        public void Cooler() => owner._viewModel.TooWarmCommand.Execute(null);
        public void Perfect() => owner._viewModel.PerfectCommand.Execute(null);
        public void Open() => owner.ShowWindow();
        public void Settings()
        {
            owner._viewModel.OpenSettingsCommand.Execute(null);
            owner.ShowWindow();
        }
        public void ShortcutHelp()
        {
            owner._viewModel.OpenSettingsCommand.Execute(null);
            owner.ShowWindow();
        }
        public void Exit() => owner._viewModel.ExitCommand.Execute(null);
    }
}
