using System.Collections.ObjectModel;
using System.Windows;
using LightPilot.Application;
using LightPilot.App.Presentation;
using LightPilot.Core;

namespace LightPilot.App.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private readonly IComfortSession _session;
    private UserSettings _settings;
    private ComfortRuntimeSnapshot _runtimeSnapshot;
    private ComfortProfileId _currentMode = ComfortProfileId.Auto;
    private int _brightnessPercent;
    private int _colorTemperatureKelvin;
    private string _reason = "Aptema is preparing your comfort";
    private string _autoStatus = "Auto on";
    private string _transitionText = "";
    private string _displaySummary = "Displays protected";
    private string _nextAdaptationText = "Next check soon";
    private string? _feedbackConfirmation;
    private bool _startWithWindows;
    private ShellSurface _selectedSurface = ShellSurface.Home;
    private SettingsViewModel? _settingsDraft;

    public MainWindowViewModel(IComfortSession session, UserSettings? initialSettings = null)
    {
        _session = session;
        _settings = initialSettings ?? session.Settings;
        _runtimeSnapshot = session.CurrentSnapshot;
        _session.SnapshotChanged += OnSnapshotChanged;

        Monitors = [];
        ToggleAutoCommand = new RelayCommand(() => _ = SetAutoEnabledAsync(!AutoEnabled));
        PauseCommand = new RelayCommand(() => _ = PauseAsync(TimeSpan.FromMinutes(30)));
        PauseThirtyMinutesCommand = new RelayCommand(() => _ = PauseAsync(TimeSpan.FromMinutes(30)));
        PauseOneHourCommand = new RelayCommand(() => _ = PauseAsync(TimeSpan.FromHours(1)));
        PauseUntilTomorrowCommand = new RelayCommand(() => _ = ExecuteAsync(token => _session.PauseUntilTomorrowAsync(token)));
        PauseResumeCommand = new RelayCommand(() =>
        {
            _ = IsPaused
                ? ExecuteAsync(token => _session.ResumeAsync(token))
                : PauseAsync(TimeSpan.FromMinutes(30));
        });
        ResumeCommand = new RelayCommand(() => _ = ExecuteAsync(token => _session.ResumeAsync(token)));
        ResetCommand = new RelayCommand(() => _ = ExecuteAsync(token => _session.ResetDefaultsAsync(token)));
        ResetComfortCommand = new RelayCommand(() => _ = ExecuteAsync(token => _session.ResetComfortAsync(token)));
        TooBrightCommand = new RelayCommand(() => _ = ApplyComfortFeedbackAsync(ComfortFeedback.TooBright));
        TooDimCommand = new RelayCommand(() => _ = ApplyComfortFeedbackAsync(ComfortFeedback.TooDim));
        TooWarmCommand = new RelayCommand(() => _ = ApplyComfortFeedbackAsync(ComfortFeedback.TooWarm));
        TooColdCommand = new RelayCommand(() => _ = ApplyComfortFeedbackAsync(ComfortFeedback.TooCold));
        PerfectCommand = new RelayCommand(() => _ = ApplyComfortFeedbackAsync(ComfortFeedback.Perfect));
        UndoFeedbackCommand = new RelayCommand(() => _ = UndoFeedbackAsync());
        SetCalmCommand = new RelayCommand(() => ComfortIntensity = 25);
        SetBalancedCommand = new RelayCommand(() => ComfortIntensity = 45);
        SetDeepComfortCommand = new RelayCommand(() => ComfortIntensity = 70);
        ShowHomeCommand = new RelayCommand(ShowHome);
        ShowQuickAdjustCommand = new RelayCommand(ShowQuickAdjust);
        SelectSurfaceCommand = new RelayCommand(parameter =>
        {
            if (parameter is ShellSurface surface)
            {
                SelectSurface(surface);
            }
        });
        OpenSettingsCommand = new RelayCommand(ShowSettingsSurface);
        SaveSettingsCommand = new RelayCommand(SaveSettingsSurface);
        CancelSettingsCommand = new RelayCommand(CancelSettingsSurface);
        ExitCommand = new RelayCommand(() => RequestExit?.Invoke(this, EventArgs.Empty));

        ProjectSnapshot(_runtimeSnapshot);
        _ = InitializeAsync();
    }

    public event EventHandler? RequestExit;
    public event EventHandler<bool>? RequestStartupRegistrationChanged;
    public event EventHandler? RequestQuickAdjust;
    public event EventHandler<FeedbackPresentationEventArgs>? FeedbackApplied;

    public ObservableCollection<MonitorStatusViewModel> Monitors { get; }

    public RelayCommand ToggleAutoCommand { get; }
    public RelayCommand PauseCommand { get; }
    public RelayCommand PauseThirtyMinutesCommand { get; }
    public RelayCommand PauseOneHourCommand { get; }
    public RelayCommand PauseUntilTomorrowCommand { get; }
    public RelayCommand PauseResumeCommand { get; }
    public RelayCommand ResumeCommand { get; }
    public RelayCommand ResetCommand { get; }
    public RelayCommand ResetComfortCommand { get; }
    public RelayCommand TooBrightCommand { get; }
    public RelayCommand TooDimCommand { get; }
    public RelayCommand TooWarmCommand { get; }
    public RelayCommand TooColdCommand { get; }
    public RelayCommand PerfectCommand { get; }
    public RelayCommand UndoFeedbackCommand { get; }
    public RelayCommand SetCalmCommand { get; }
    public RelayCommand SetBalancedCommand { get; }
    public RelayCommand SetDeepComfortCommand { get; }
    public RelayCommand ShowHomeCommand { get; }
    public RelayCommand ShowQuickAdjustCommand { get; }
    public RelayCommand SelectSurfaceCommand { get; }
    public RelayCommand OpenSettingsCommand { get; }
    public RelayCommand SaveSettingsCommand { get; }
    public RelayCommand CancelSettingsCommand { get; }
    public RelayCommand ExitCommand { get; }

    public UserSettings Settings => _settings;

    public ComfortRuntimeSnapshot RuntimeSnapshot
    {
        get => _runtimeSnapshot;
        private set => SetProperty(ref _runtimeSnapshot, value);
    }

    public bool StartWithWindows
    {
        get => _startWithWindows;
        set => SetProperty(ref _startWithWindows, value);
    }

    public bool AutoEnabled
    {
        get => _settings.AutoEnabled;
        set
        {
            if (_settings.AutoEnabled != value)
            {
                _settings = _settings with { AutoEnabled = value };
                NotifySettingsChanged();
                _ = SetAutoEnabledAsync(value);
            }
        }
    }

    public int ComfortIntensity
    {
        get => _settings.ComfortIntensity;
        set
        {
            var clamped = Math.Clamp(value, 0, 100);
            if (_settings.ComfortIntensity != clamped)
            {
                ApplySettingsToSession(_settings with { ComfortIntensity = clamped });
            }
        }
    }

    public ComfortProfileId CurrentMode
    {
        get => _currentMode;
        private set => SetProperty(ref _currentMode, value);
    }

    public int BrightnessPercent
    {
        get => _brightnessPercent;
        private set => SetProperty(ref _brightnessPercent, value);
    }

    public int ColorTemperatureKelvin
    {
        get => _colorTemperatureKelvin;
        private set => SetProperty(ref _colorTemperatureKelvin, value);
    }

    public string Reason
    {
        get => _reason;
        private set => SetProperty(ref _reason, value);
    }

    public string AutoStatus
    {
        get => _autoStatus;
        private set => SetProperty(ref _autoStatus, value);
    }

    public string TransitionText
    {
        get => _transitionText;
        private set => SetProperty(ref _transitionText, value);
    }

    public string DisplaySummary
    {
        get => _displaySummary;
        private set => SetProperty(ref _displaySummary, value);
    }

    public string NextAdaptationText
    {
        get => _nextAdaptationText;
        private set => SetProperty(ref _nextAdaptationText, value);
    }

    public IReadOnlyList<ShellNavigationItem> NavigationItems { get; } = ShellNavigation.Build();

    public ShellSurface SelectedSurface
    {
        get => _selectedSurface;
        private set
        {
            if (SetProperty(ref _selectedSurface, value))
            {
                OnPropertyChanged(nameof(HomeVisibility));
                OnPropertyChanged(nameof(QuickAdjustVisibility));
                OnPropertyChanged(nameof(SettingsVisibility));
                OnPropertyChanged(nameof(CurrentSurface));
            }
        }
    }

    public ShellNavigationItem CurrentSurface => ShellNavigation.Get(SelectedSurface);
    public Visibility HomeVisibility => SelectedSurface == ShellSurface.Home ? Visibility.Visible : Visibility.Collapsed;
    public Visibility QuickAdjustVisibility => Visibility.Collapsed;
    public Visibility SettingsVisibility => SelectedSurface == ShellSurface.System ? Visibility.Visible : Visibility.Collapsed;

    public SettingsViewModel? SettingsDraft
    {
        get => _settingsDraft;
        private set => SetProperty(ref _settingsDraft, value);
    }

    public string BrightnessText => ComfortCopy.DescribeLightLevel(BrightnessPercent);
    public string WarmthText => ComfortCopy.DescribeWarmth(ColorTemperatureKelvin);
    public string ComfortIntensityText => ComfortCopy.DescribeIntensity(ComfortIntensity);
    public bool CanUndoFeedback => RuntimeSnapshot.FeedbackUndoAvailableUntil > DateTimeOffset.UtcNow;
    public bool IsPaused => !_settings.AutoEnabled || RuntimeSnapshot.PausedUntil is not null;
    public string PrimaryPauseText => IsPaused ? "Resume" : "Pause 30 min";
    public bool ShortcutAvailable { get; private set; } = true;
    public string ShortcutStatusText => ShortcutAvailable
        ? "Quick Adjust shortcut: Win+Alt+A"
        : "Win+Alt+A is already used by another app. Open Adjust from the tray or change that app's shortcut.";
    public string ActiveApplicationText => ApplicationDisplayNamePolicy.GetDisplayName(RuntimeSnapshot.AppContext.ProcessName);

    public void SetQuickAdjustShortcutAvailability(bool available)
    {
        if (ShortcutAvailable == available)
        {
            return;
        }

        ShortcutAvailable = available;
        OnPropertyChanged(nameof(ShortcutAvailable));
        OnPropertyChanged(nameof(ShortcutStatusText));
    }

    public string CurrentModeText => CurrentMode switch
    {
        ComfortProfileId.Reading when GetDayPart() == "Evening" => "Evening Reading",
        ComfortProfileId.Reading => "Reading",
        ComfortProfileId.Development when GetDayPart() == "Night" => "Late Development",
        ComfortProfileId.Evening => "Evening",
        ComfortProfileId.Night => "Night",
        ComfortProfileId.Day => "Day",
        _ => CurrentMode.ToString()
    };

    public string ComfortStateText => !_settings.AutoEnabled
        ? "Comfort is paused"
        : RuntimeSnapshot.PausedUntil is not null
            ? "Paused for now"
            : "Comfortable now";

    public void ApplySettings(UserSettings settings, bool startWithWindows)
    {
        StartWithWindows = startWithWindows;
        ApplySettingsToSession(settings);
    }

    public Task RefreshDecisionAsync() => ExecuteAsync(token => _session.RequestRefreshAsync(token));

    private async Task InitializeAsync()
    {
        try
        {
            await _session.StartAsync(_settings, CancellationToken.None).ConfigureAwait(false);
            OnSnapshotChanged(_session.CurrentSnapshot);
        }
        catch (Exception)
        {
            RunOnUi(() => Reason = "Comfort service unavailable; no changes applied");
        }
    }

    private void OnSnapshotChanged(ComfortRuntimeSnapshot snapshot)
    {
        RunOnUi(() => ProjectSnapshot(snapshot));
    }

    private void ProjectSnapshot(ComfortRuntimeSnapshot snapshot)
    {
        RuntimeSnapshot = snapshot;
        OnPropertyChanged(nameof(CanUndoFeedback));
        OnPropertyChanged(nameof(IsPaused));
        OnPropertyChanged(nameof(PrimaryPauseText));
        OnPropertyChanged(nameof(ActiveApplicationText));
        _settings = _session.Settings;
        NotifySettingsChanged();
        ProjectDisplays(snapshot.Displays);

        var decision = snapshot.PrimaryDecision;
        if (decision is not null)
        {
            CurrentMode = decision.Profile;
            if (decision.ShouldApply || BrightnessPercent == 0)
            {
                BrightnessPercent = decision.TargetBrightnessPercent;
                ColorTemperatureKelvin = decision.TargetColorTemperatureKelvin;
            }

            Reason = _feedbackConfirmation ?? (decision.Reason == "Comfort cleared"
                ? "Comfort cleared for 30 minutes"
                : ComfortCopy.DescribeReason(decision));
            _feedbackConfirmation = null;
            TransitionText = decision.Reason == "Comfort cleared"
                ? "Clear"
                : decision.Source == DecisionSource.Manual
                    ? "Learning"
                    : decision.ShouldApply ? "Adjusting gently" : "Comfort steady";
        }

        AutoStatus = BuildAutoStatus();
        DisplaySummary = BuildDisplaySummary(snapshot.Displays);
        var interval = RefreshCadencePolicy.GetInterval(_settings, snapshot.PausedUntil is not null, _settings.EnableContentBrightnessAnalysis);
        NextAdaptationText = $"Next check in {(int)interval.TotalSeconds}s";
        NotifyDecisionChanged();
    }

    private void ProjectDisplays(IReadOnlyList<DisplayRuntimeState> displays)
    {
        if (Monitors.Count != displays.Count || Monitors.Where((item, index) => item.Name != displays[index].Monitor.Name).Any())
        {
            Monitors.Clear();
            foreach (var display in displays)
            {
                Monitors.Add(new MonitorStatusViewModel(display.Monitor.Name));
            }
        }

        for (var index = 0; index < displays.Count; index++)
        {
            var display = displays[index];
            var status = Monitors[index];
            if (display.ApplyResult.State == MonitorControlState.Disabled)
            {
                status.Status = "Off";
                status.LightLevel = "Not controlled";
                status.ControlLayer = "Disabled";
                continue;
            }

            if (display.Decision.ShouldApply || status.BrightnessPercent == 0)
            {
                status.BrightnessPercent = display.Decision.TargetBrightnessPercent;
                status.ColorTemperatureKelvin = display.Monitor.SupportsColorTemperature
                    ? display.Decision.TargetColorTemperatureKelvin
                    : 6500;
            }

            status.ControlLayer = GetControlLayer(display.Monitor, display.ApplyResult);
            status.Status = GetMonitorStatusText(display.ApplyResult);
            status.LightLevel = ComfortCopy.DescribeLightLevel(status.BrightnessPercent);
        }
    }

    private async Task SetAutoEnabledAsync(bool enabled)
    {
        await ExecuteAsync(token => _session.SetAutoEnabledAsync(enabled, token)).ConfigureAwait(false);
    }

    private Task PauseAsync(TimeSpan duration) => ExecuteAsync(token => _session.PauseForAsync(duration, token));

    private async Task ApplyComfortFeedbackAsync(ComfortFeedback feedback)
    {
        _feedbackConfirmation = feedback switch
        {
            ComfortFeedback.TooBright => "Got it. Softer from here.",
            ComfortFeedback.TooDim => "Got it. Keeping it clearer.",
            ComfortFeedback.TooWarm => "Got it. Less warm.",
            ComfortFeedback.TooCold => "Got it. A little warmer.",
            ComfortFeedback.Perfect => "Saved. Aptema will remember this.",
            _ => "Saved."
        };
        try
        {
            await _session.ApplyFeedbackAsync(feedback, CancellationToken.None).ConfigureAwait(false);
            var message = feedback switch
            {
                ComfortFeedback.TooBright => "A little softer",
                ComfortFeedback.TooDim => "A little clearer",
                ComfortFeedback.TooWarm => "Less warm",
                ComfortFeedback.TooCold => "A little warmer",
                ComfortFeedback.Perfect => "Comfort remembered",
                _ => "Comfort updated"
            };
            RunOnUi(() => FeedbackApplied?.Invoke(this, new FeedbackPresentationEventArgs(message, _session.CurrentSnapshot.FeedbackUndoAvailableUntil > DateTimeOffset.UtcNow)));
        }
        catch (Exception)
        {
            RunOnUi(() => Reason = "Comfort service unavailable; no changes applied");
        }
    }

    private async Task UndoFeedbackAsync()
    {
        try
        {
            var result = await _session.UndoFeedbackAsync(CancellationToken.None).ConfigureAwait(false);
            if (result.IsUsable)
            {
                RunOnUi(() => FeedbackApplied?.Invoke(this, new FeedbackPresentationEventArgs("Last adjustment undone", false)));
            }
        }
        catch (Exception)
        {
            RunOnUi(() => Reason = "That adjustment could not be undone");
        }
    }

    private void ApplySettingsToSession(UserSettings settings)
    {
        _settings = settings;
        NotifySettingsChanged();
        _ = ExecuteAsync(token => _session.ApplySettingsAsync(settings, token));
    }

    private async Task ExecuteAsync(Func<CancellationToken, ValueTask> operation)
    {
        try
        {
            await operation(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception)
        {
            RunOnUi(() => Reason = "Comfort service unavailable; no changes applied");
        }
    }

    private void ShowHome() => SelectedSurface = ShellSurface.Home;
    private void ShowQuickAdjust() => RequestQuickAdjust?.Invoke(this, EventArgs.Empty);

    private void SelectSurface(ShellSurface surface)
    {
        if (surface == ShellSurface.System && SettingsDraft is null)
        {
            SettingsDraft = new SettingsViewModel(_settings, StartWithWindows);
        }

        SelectedSurface = surface;
    }

    private void ShowSettingsSurface()
    {
        SettingsDraft = new SettingsViewModel(_settings, StartWithWindows);
        SelectedSurface = ShellSurface.System;
    }

    private void SaveSettingsSurface()
    {
        if (SettingsDraft is null)
        {
            SelectedSurface = ShellSurface.Home;
            return;
        }

        var startWithWindows = SettingsDraft.StartWithWindows;
        ApplySettings(SettingsDraft.ToSettings(_settings), startWithWindows);
        RequestStartupRegistrationChanged?.Invoke(this, startWithWindows);
        SettingsDraft = null;
        SelectedSurface = ShellSurface.Home;
    }

    private void CancelSettingsSurface()
    {
        SettingsDraft = null;
        SelectedSurface = ShellSurface.Home;
    }

    private void NotifySettingsChanged()
    {
        OnPropertyChanged(nameof(Settings));
        OnPropertyChanged(nameof(AutoEnabled));
        OnPropertyChanged(nameof(ComfortIntensity));
        OnPropertyChanged(nameof(ComfortIntensityText));
        OnPropertyChanged(nameof(ComfortStateText));
        OnPropertyChanged(nameof(IsPaused));
        OnPropertyChanged(nameof(PrimaryPauseText));
    }

    private void NotifyDecisionChanged()
    {
        OnPropertyChanged(nameof(BrightnessText));
        OnPropertyChanged(nameof(WarmthText));
        OnPropertyChanged(nameof(CurrentModeText));
        OnPropertyChanged(nameof(ComfortStateText));
    }

    private string BuildAutoStatus()
    {
        if (!_settings.AutoEnabled)
        {
            return "Auto off";
        }

        return RuntimeSnapshot.PausedUntil is { } until
            ? $"Paused until {until.LocalDateTime:t}"
            : "Auto active";
    }

    private static string BuildDisplaySummary(IReadOnlyList<DisplayRuntimeState> displays)
    {
        var active = displays.Count(item => item.ApplyResult.State != MonitorControlState.Disabled);
        var fallback = displays.Count(item => item.ApplyResult.State is MonitorControlState.FallbackUsed or MonitorControlState.Degraded or MonitorControlState.Failed);
        var protectedDisplays = displays.Count(item => item.ApplyResult.State == MonitorControlState.Protected);
        if (active == 0)
        {
            return "No displays controlled";
        }

        if (fallback > 0)
        {
            return fallback == 1 ? "1 display using fallback" : $"{fallback} displays using fallback";
        }

        if (protectedDisplays > 0)
        {
            return protectedDisplays == 1 ? "1 display protected" : $"{protectedDisplays} displays protected";
        }

        return active == 1 ? "1 display protected" : $"{active} displays protected";
    }

    private string GetControlLayer(MonitorModel monitor, BrightnessApplyResult result)
    {
        return result.AppliedLayer switch
        {
            BrightnessControlLayer.DdcCi => "DDC/CI",
            BrightnessControlLayer.WindowsBrightness => "Windows brightness",
            BrightnessControlLayer.Overlay => "Overlay fallback",
            BrightnessControlLayer.None when _settings.EnableDdcCi && monitor.SupportsBrightnessControl => "DDC/CI",
            BrightnessControlLayer.None => monitor.SupportsBrightnessControl ? "Windows brightness" : "Overlay fallback",
            _ => "Fallback"
        };
    }

    private string GetMonitorStatusText(BrightnessApplyResult result)
    {
        if (!_settings.AutoEnabled || RuntimeSnapshot.PausedUntil is not null)
        {
            return "Paused";
        }

        return result.State switch
        {
            MonitorControlState.Degraded or MonitorControlState.FallbackUsed or MonitorControlState.Failed => "Fallback",
            MonitorControlState.Throttled => "Smoothing",
            MonitorControlState.Protected => "Protected",
            MonitorControlState.Disabled => "Off",
            _ => "Ready"
        };
    }

    private static void RunOnUi(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            dispatcher.BeginInvoke(action);
        }
    }

    private static string GetDayPart()
    {
        var now = DateTimeOffset.Now.TimeOfDay;
        if (now >= TimeSpan.FromHours(17.5) && now < TimeSpan.FromHours(21.5))
        {
            return "Evening";
        }

        return now < TimeSpan.FromHours(8) || now >= TimeSpan.FromHours(21.5) ? "Night" : "Day";
    }
}

public sealed record FeedbackPresentationEventArgs(string Message, bool CanUndo);
