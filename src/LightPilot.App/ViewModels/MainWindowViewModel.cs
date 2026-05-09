using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using LightPilot.Core;
using LightPilot.Infrastructure;

namespace LightPilot.App.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private readonly AdaptiveEngine _engine = new();
    private readonly ISettingsStore _settingsStore;
    private readonly IMonitorEnumerator _monitorEnumerator;
    private readonly IForegroundWindowDetector _foregroundWindowDetector;
    private readonly IContentLuminanceSampler _contentLuminanceSampler;
    private readonly IBrightnessController _brightnessController;
    private readonly IPowerStatusProvider _powerStatusProvider;
    private readonly DispatcherTimer _timer;
    private readonly List<MonitorModel> _monitorModels = [];
    private readonly Dictionary<string, AdaptiveEngineState> _engineStates = new(StringComparer.OrdinalIgnoreCase);

    private UserSettings _settings = UserSettings.Default;
    private DateTimeOffset? _pauseUntil;
    private ComfortProfileId _currentMode = ComfortProfileId.Auto;
    private int _brightnessPercent;
    private int _colorTemperatureKelvin;
    private string _reason = "Starting Light Pilot";
    private string _autoStatus = "Auto on";
    private string _transitionText = "";
    private string _displaySummary = "Displays protected";
    private string _nextAdaptationText = "Next check soon";
    private bool _startWithWindows;
    private bool _refreshInProgress;
    private readonly bool _hasInitialSettings;
    private CancellationTokenSource? _settingsSaveCts;
    private MainSurface _selectedSurface = MainSurface.Home;
    private SettingsViewModel? _settingsDraft;
    private AppContextModel _lastAppContext = new("unknown.exe", AppCategory.Unknown, false);
    private ContentLuminanceSample _lastContent = ContentLuminanceSample.Unknown;

    public MainWindowViewModel(
        ISettingsStore settingsStore,
        IMonitorEnumerator monitorEnumerator,
        IForegroundWindowDetector foregroundWindowDetector,
        IContentLuminanceSampler contentLuminanceSampler,
        IBrightnessController brightnessController,
        IPowerStatusProvider powerStatusProvider,
        UserSettings? initialSettings = null)
    {
        _settingsStore = settingsStore;
        _monitorEnumerator = monitorEnumerator;
        _foregroundWindowDetector = foregroundWindowDetector;
        _contentLuminanceSampler = contentLuminanceSampler;
        _brightnessController = brightnessController;
        _powerStatusProvider = powerStatusProvider;
        _settings = initialSettings ?? UserSettings.Default;
        _hasInitialSettings = initialSettings is not null;

        Monitors = [];

        ToggleAutoCommand = new RelayCommand(ToggleAuto);
        PauseCommand = new RelayCommand(PauseThirtyMinutes);
        PauseThirtyMinutesCommand = new RelayCommand(PauseThirtyMinutes);
        PauseOneHourCommand = new RelayCommand(PauseOneHour);
        PauseUntilTomorrowCommand = new RelayCommand(PauseUntilTomorrow);
        ResumeCommand = new RelayCommand(ResumeAuto);
        ResetCommand = new RelayCommand(ResetDefaults);
        ResetComfortCommand = new RelayCommand(() => _ = ResetComfortNowAsync());
        TooBrightCommand = new RelayCommand(() => ApplyComfortFeedback(ComfortFeedback.TooBright));
        TooDimCommand = new RelayCommand(() => ApplyComfortFeedback(ComfortFeedback.TooDim));
        TooWarmCommand = new RelayCommand(() => ApplyComfortFeedback(ComfortFeedback.TooWarm));
        TooColdCommand = new RelayCommand(() => ApplyComfortFeedback(ComfortFeedback.TooCold));
        PerfectCommand = new RelayCommand(() => ApplyComfortFeedback(ComfortFeedback.Perfect));
        SetCalmCommand = new RelayCommand(() => SetIntensityPreset(25));
        SetBalancedCommand = new RelayCommand(() => SetIntensityPreset(45));
        SetDeepComfortCommand = new RelayCommand(() => SetIntensityPreset(70));
        ShowHomeCommand = new RelayCommand(ShowHome);
        ShowQuickAdjustCommand = new RelayCommand(ShowQuickAdjust);
        OpenSettingsCommand = new RelayCommand(ShowSettingsSurface);
        SaveSettingsCommand = new RelayCommand(SaveSettingsSurface);
        CancelSettingsCommand = new RelayCommand(ShowHome);
        ExitCommand = new RelayCommand(() => RequestExit?.Invoke(this, EventArgs.Empty));

        _timer = new DispatcherTimer { Interval = RefreshCadencePolicy.GetInterval(_settings, isPaused: false, isContentAnalysisEnabled: false) };
        _timer.Tick += async (_, _) => await RefreshDecisionAsync().ConfigureAwait(true);

        _ = InitializeAsync();
    }

    public event EventHandler? RequestExit;
    public event EventHandler<bool>? RequestStartupRegistrationChanged;

    public ObservableCollection<MonitorStatusViewModel> Monitors { get; }

    public RelayCommand ToggleAutoCommand { get; }
    public RelayCommand PauseCommand { get; }
    public RelayCommand PauseThirtyMinutesCommand { get; }
    public RelayCommand PauseOneHourCommand { get; }
    public RelayCommand PauseUntilTomorrowCommand { get; }
    public RelayCommand ResumeCommand { get; }
    public RelayCommand ResetCommand { get; }
    public RelayCommand ResetComfortCommand { get; }
    public RelayCommand TooBrightCommand { get; }
    public RelayCommand TooDimCommand { get; }
    public RelayCommand TooWarmCommand { get; }
    public RelayCommand TooColdCommand { get; }
    public RelayCommand PerfectCommand { get; }
    public RelayCommand SetCalmCommand { get; }
    public RelayCommand SetBalancedCommand { get; }
    public RelayCommand SetDeepComfortCommand { get; }
    public RelayCommand ShowHomeCommand { get; }
    public RelayCommand ShowQuickAdjustCommand { get; }
    public RelayCommand OpenSettingsCommand { get; }
    public RelayCommand SaveSettingsCommand { get; }
    public RelayCommand CancelSettingsCommand { get; }
    public RelayCommand ExitCommand { get; }

    public UserSettings Settings => _settings;

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
            if (_settings.AutoEnabled == value)
            {
                return;
            }

            if (value)
            {
                _pauseUntil = null;
            }

            UpdateSettings(_settings with { AutoEnabled = value }, persist: true);
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
                UpdateSettings(_settings with { ComfortIntensity = clamped }, persist: true);
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

    public MainSurface SelectedSurface
    {
        get => _selectedSurface;
        private set
        {
            if (SetProperty(ref _selectedSurface, value))
            {
                OnPropertyChanged(nameof(HomeVisibility));
                OnPropertyChanged(nameof(QuickAdjustVisibility));
                OnPropertyChanged(nameof(SettingsVisibility));
            }
        }
    }

    public Visibility HomeVisibility => SelectedSurface == MainSurface.Home ? Visibility.Visible : Visibility.Collapsed;

    public Visibility QuickAdjustVisibility => SelectedSurface == MainSurface.QuickAdjust ? Visibility.Visible : Visibility.Collapsed;

    public Visibility SettingsVisibility => SelectedSurface == MainSurface.Settings ? Visibility.Visible : Visibility.Collapsed;

    public SettingsViewModel? SettingsDraft
    {
        get => _settingsDraft;
        private set => SetProperty(ref _settingsDraft, value);
    }

    public string BrightnessText => ComfortCopy.DescribeLightLevel(BrightnessPercent);

    public string WarmthText => ComfortCopy.DescribeWarmth(ColorTemperatureKelvin);

    public string ComfortIntensityText => ComfortCopy.DescribeIntensity(ComfortIntensity);

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

    public string ComfortStateText
    {
        get
        {
            if (!_settings.AutoEnabled)
            {
                return "Comfort is paused";
            }

            if (_pauseUntil is not null)
            {
                return "Paused for now";
            }

            return "Comfortable now";
        }
    }

    public void ApplySettings(UserSettings settings, bool startWithWindows)
    {
        StartWithWindows = startWithWindows;
        UpdateSettings(settings, persist: false);
        _ = PersistSettingsAsync(settings, immediate: true);
    }

    private async Task InitializeAsync()
    {
        try
        {
            if (!_hasInitialSettings)
            {
                _settings = await _settingsStore.LoadAsync(CancellationToken.None).ConfigureAwait(true);
            }
            OnPropertyChanged(nameof(Settings));
            OnPropertyChanged(nameof(AutoEnabled));
            OnPropertyChanged(nameof(ComfortIntensity));
            UpdateTimerInterval();
            await ReloadMonitorsAsync().ConfigureAwait(true);
            await RefreshDecisionAsync().ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Reason = "Settings unavailable; using safe defaults";
        }

        _timer.Start();
    }

    private async Task ReloadMonitorsAsync()
    {
        IReadOnlyList<MonitorModel> monitors;
        try
        {
            monitors = await _monitorEnumerator.EnumerateAsync(CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            monitors = [new MonitorModel("primary", "Primary display", false, true, 15, 100, 0)];
        }

        _monitorModels.Clear();
        _monitorModels.AddRange(ApplyMonitorPreferences(monitors, _settings));
        Monitors.Clear();
        foreach (var monitor in _monitorModels)
        {
            Monitors.Add(new MonitorStatusViewModel(monitor.Name));
            _engineStates.TryAdd(monitor.Id, AdaptiveEngineState.Empty);
        }
    }

    public async Task RefreshDecisionAsync()
    {
        if (_refreshInProgress)
        {
            return;
        }

        _refreshInProgress = true;
        try
        {
            if (_pauseUntil <= DateTimeOffset.Now)
            {
                _pauseUntil = null;
            }

            if (_monitorModels.Count == 0)
            {
                await ReloadMonitorsAsync().ConfigureAwait(true);
            }

            var isPaused = _pauseUntil is not null;
            var batteryAware = PowerAwareSettingsPolicy.Apply(_settings, _powerStatusProvider.IsOnBattery());
            var effectiveSettings = isPaused ? batteryAware with { AutoEnabled = false } : batteryAware;
            var appContext = effectiveSettings.AutoEnabled
                ? await Task.Run(() => SafeDetectContext()).ConfigureAwait(true)
                : new AppContextModel("LightPilot.App", AppCategory.System, false);
            appContext = ApplyAppOverrides(appContext, effectiveSettings);
            var shouldSampleContent = effectiveSettings.EnableContentBrightnessAnalysis &&
                appContext.Category is AppCategory.Browser or AppCategory.EmailCommunication or AppCategory.OfficeReading;
            var content = effectiveSettings.AutoEnabled
                ? await SampleContentAsync(shouldSampleContent).ConfigureAwait(true)
                : ContentLuminanceSample.Unknown;
            _lastAppContext = appContext;
            _lastContent = content;

            ComfortDecision? primaryDecision = null;
            var fallbackDisplays = 0;
            var protectedDisplays = 0;
            for (var index = 0; index < _monitorModels.Count; index++)
            {
                var monitor = _monitorModels[index];
                if (IsMonitorDisabled(monitor, _settings))
                {
                    UpdateDisabledMonitorStatus(index);
                    continue;
                }

                var currentBrightness = BrightnessPercent == 0 ? 62 : BrightnessPercent;
                var currentKelvin = ColorTemperatureKelvin == 0 ? 5200 : ColorTemperatureKelvin;
                var snapshot = CreateSnapshot(monitor, appContext, content, currentBrightness, currentKelvin);
                var state = _engineStates.GetValueOrDefault(monitor.Id, AdaptiveEngineState.Empty);
                var decision = _engine.Evaluate(snapshot, state, effectiveSettings);

                var applyResult = await _brightnessController.ApplyAsync(monitor, decision, effectiveSettings, CancellationToken.None).ConfigureAwait(true);
                if (applyResult.State is MonitorControlState.FallbackUsed or MonitorControlState.Degraded or MonitorControlState.Failed)
                {
                    fallbackDisplays++;
                }

                if (applyResult.State == MonitorControlState.Protected)
                {
                    protectedDisplays++;
                }

                if (decision.ShouldApply)
                {
                    _engineStates[monitor.Id] = state with
                    {
                        LastAppliedAt = snapshot.Now,
                        LastDecision = decision.Target
                    };
                }

                UpdateMonitorStatus(index, monitor, decision, effectiveSettings, applyResult);
                primaryDecision ??= decision;
            }

            if (primaryDecision is not null)
            {
                CurrentMode = primaryDecision.Profile;
                if (primaryDecision.ShouldApply || BrightnessPercent == 0)
                {
                    BrightnessPercent = primaryDecision.TargetBrightnessPercent;
                    ColorTemperatureKelvin = primaryDecision.TargetColorTemperatureKelvin;
                }

                Reason = ComfortCopy.DescribeReason(primaryDecision);
                TransitionText = primaryDecision.ShouldApply ? "Adjusting gently" : "Comfort steady";
            }

            AutoStatus = BuildAutoStatus();
            DisplaySummary = BuildDisplaySummary(fallbackDisplays, protectedDisplays);
            NextAdaptationText = $"Next check in {(int)_timer.Interval.TotalSeconds}s";
            UpdateTimerInterval();
            OnPropertyChanged(nameof(BrightnessText));
            OnPropertyChanged(nameof(WarmthText));
            OnPropertyChanged(nameof(ComfortIntensityText));
            OnPropertyChanged(nameof(CurrentModeText));
            OnPropertyChanged(nameof(ComfortStateText));
        }
        finally
        {
            _refreshInProgress = false;
        }
    }

    private AppContextModel SafeDetectContext()
    {
        try
        {
            return _foregroundWindowDetector.Detect();
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return new AppContextModel("unknown.exe", AppCategory.Unknown, false);
        }
    }

    private static AppContextModel ApplyAppOverrides(AppContextModel appContext, UserSettings settings)
    {
        if (string.IsNullOrWhiteSpace(appContext.ProcessName))
        {
            return appContext;
        }

        var mapper = AppCategoryMapper.CreateDefault(settings.AppOverrides);
        var category = mapper.Classify(appContext.ProcessName);
        return category == appContext.Category ? appContext : appContext with { Category = category };
    }

    private static IReadOnlyList<MonitorModel> ApplyMonitorPreferences(IReadOnlyList<MonitorModel> monitors, UserSettings settings)
    {
        return monitors
            .Select(monitor =>
            {
                var preference = settings.MonitorPreferences.FirstOrDefault(item => string.Equals(item.MonitorId, monitor.Id, StringComparison.OrdinalIgnoreCase));
                return preference is null
                    ? monitor
                    : monitor with { BrightnessOffsetPercent = preference.BrightnessOffsetPercent };
            })
            .ToArray();
    }

    private static bool IsMonitorDisabled(MonitorModel monitor, UserSettings settings)
    {
        return settings.MonitorPreferences.Any(item =>
            item.IsDisabled && string.Equals(item.MonitorId, monitor.Id, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<ContentLuminanceSample> SampleContentAsync(bool enabled)
    {
        try
        {
            return await Task.Run(async () => await _contentLuminanceSampler.SampleAsync(enabled, CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ExternalException)
        {
            return ContentLuminanceSample.Unknown;
        }
    }

    private AdaptiveSnapshot CreateSnapshot(MonitorModel monitor, AppContextModel appContext, ContentLuminanceSample content, int currentBrightness, int currentKelvin)
    {
        return new AdaptiveSnapshot(
            DateTimeOffset.Now,
            monitor,
            appContext,
            content,
            TimeSpan.FromMinutes(42),
            currentBrightness,
            currentKelvin,
            _pauseUntil);
    }

    private void UpdateMonitorStatus(int index, MonitorModel monitor, ComfortDecision decision, UserSettings effectiveSettings, BrightnessApplyResult applyResult)
    {
        var status = Monitors[index];
        if (decision.ShouldApply || status.BrightnessPercent == 0)
        {
            status.BrightnessPercent = decision.TargetBrightnessPercent;
            status.ColorTemperatureKelvin = monitor.SupportsColorTemperature ? decision.TargetColorTemperatureKelvin : 6500;
        }

        status.ControlLayer = GetControlLayer(monitor, effectiveSettings, applyResult);
        status.Status = GetMonitorStatusText(effectiveSettings, applyResult);
        status.LightLevel = ComfortCopy.DescribeLightLevel(status.BrightnessPercent);
    }

    private void UpdateDisabledMonitorStatus(int index)
    {
        var status = Monitors[index];
        status.Status = "Off";
        status.LightLevel = "Not controlled";
        status.ControlLayer = "Disabled";
    }

    private string BuildAutoStatus()
    {
        if (!_settings.AutoEnabled)
        {
            return "Auto off";
        }

        if (_pauseUntil is { } until)
        {
            return $"Paused until {until.LocalDateTime:t}";
        }

        return "Auto active";
    }

    private string BuildDisplaySummary(int fallbackDisplays, int protectedDisplays)
    {
        var active = _monitorModels.Count(monitor => !IsMonitorDisabled(monitor, _settings));
        if (active <= 0)
        {
            return "No displays controlled";
        }

        if (fallbackDisplays > 0)
        {
            return fallbackDisplays == 1 ? "1 display using fallback" : $"{fallbackDisplays} displays using fallback";
        }

        if (protectedDisplays > 0)
        {
            return protectedDisplays == 1 ? "1 display protected" : $"{protectedDisplays} displays protected";
        }

        return active == 1 ? "1 display protected" : $"{active} displays protected";
    }

    private static string GetControlLayer(MonitorModel monitor, UserSettings settings, BrightnessApplyResult result)
    {
        return result.AppliedLayer switch
        {
            BrightnessControlLayer.DdcCi => "DDC/CI",
            BrightnessControlLayer.WindowsBrightness => "Windows brightness",
            BrightnessControlLayer.Overlay => "Overlay fallback",
            BrightnessControlLayer.None when settings.EnableDdcCi && monitor.SupportsBrightnessControl => "DDC/CI",
            BrightnessControlLayer.None => monitor.SupportsBrightnessControl ? "Windows brightness" : "Overlay fallback",
            _ => "Fallback"
        };
    }

    private static string GetMonitorStatusText(UserSettings effectiveSettings, BrightnessApplyResult result)
    {
        if (!effectiveSettings.AutoEnabled)
        {
            return "Paused";
        }

        return result.State switch
        {
            MonitorControlState.Degraded => "Fallback",
            MonitorControlState.FallbackUsed => "Fallback",
            MonitorControlState.Failed => "Fallback",
            MonitorControlState.Throttled => "Smoothing",
            MonitorControlState.Protected => "Protected",
            MonitorControlState.Disabled => "Off",
            _ => "Ready"
        };
    }

    private void ToggleAuto()
    {
        AutoEnabled = !AutoEnabled;
    }

    private void PauseThirtyMinutes()
    {
        if (!_settings.AutoEnabled)
        {
            AutoEnabled = true;
        }

        _pauseUntil = DateTimeOffset.Now.AddMinutes(30);
        UpdateTimerInterval();
        _ = RefreshDecisionAsync();
    }

    private void PauseOneHour()
    {
        if (!_settings.AutoEnabled)
        {
            AutoEnabled = true;
        }

        _pauseUntil = DateTimeOffset.Now.AddHours(1);
        UpdateTimerInterval();
        _ = RefreshDecisionAsync();
    }

    private void PauseUntilTomorrow()
    {
        if (!_settings.AutoEnabled)
        {
            AutoEnabled = true;
        }

        var tomorrowWake = DateTime.Today.AddDays(1).Add(_settings.WakeTime.ToTimeSpan());
        _pauseUntil = new DateTimeOffset(tomorrowWake);
        UpdateTimerInterval();
        _ = RefreshDecisionAsync();
    }

    private void ResumeAuto()
    {
        _pauseUntil = null;
        if (!_settings.AutoEnabled)
        {
            _settings = _settings with { AutoEnabled = true };
            _ = PersistSettingsAsync(_settings, immediate: false);
        }

        OnPropertyChanged(nameof(AutoEnabled));
        UpdateTimerInterval();
        _ = RefreshDecisionAsync();
    }

    private void ResetDefaults()
    {
        _pauseUntil = null;
        UpdateSettings(UserSettings.Default, persist: true);
    }

    private async Task ResetComfortNowAsync()
    {
        _pauseUntil = DateTimeOffset.Now.AddMinutes(30);
        var brightness = BrightnessPercent == 0 ? 62 : BrightnessPercent;
        var decision = new ComfortDecision(ComfortProfileId.Paused, brightness, 6500, 0, TimeSpan.FromSeconds(2), true, "Comfort cleared", new[] { "ComfortCleared" });

        foreach (var monitor in _monitorModels)
        {
            await _brightnessController.ApplyAsync(monitor, decision, _settings, CancellationToken.None).ConfigureAwait(true);
        }

        ColorTemperatureKelvin = 6500;
        Reason = "Comfort cleared for 30 minutes";
        TransitionText = "Clear";
        AutoStatus = BuildAutoStatus();
        OnPropertyChanged(nameof(WarmthText));
    }

    private void ApplyComfortFeedback(ComfortFeedback feedback)
    {
        _ = ApplyComfortFeedbackAsync(feedback);
    }

    private async Task ApplyComfortFeedbackAsync(ComfortFeedback feedback)
    {
        var now = DateTimeOffset.Now;
        var monitor = _monitorModels.FirstOrDefault() ?? new MonitorModel("primary", "Primary display", false, true, 15, 100, 0);
        var currentBrightness = BrightnessPercent == 0 ? 62 : BrightnessPercent;
        var currentKelvin = ColorTemperatureKelvin == 0 ? 5200 : ColorTemperatureKelvin;
        var snapshot = CreateSnapshot(monitor, _lastAppContext, _lastContent, currentBrightness, currentKelvin);
        var context = PreferenceLearningContext.FromSnapshot(snapshot);
        var updated = ComfortPreferenceAdvisor.Apply(_settings, feedback, context, now);
        UpdateSettings(updated, persist: true);

        var decision = _engine.EvaluateManualFeedback(snapshot, updated, feedback);
        foreach (var display in _monitorModels)
        {
            await _brightnessController.ApplyAsync(display, decision, updated, CancellationToken.None).ConfigureAwait(true);
        }

        if (feedback != ComfortFeedback.Perfect)
        {
            BrightnessPercent = decision.TargetBrightnessPercent;
            ColorTemperatureKelvin = decision.TargetColorTemperatureKelvin;
        }

        Reason = feedback switch
        {
            ComfortFeedback.TooBright => "Got it. Softer from here.",
            ComfortFeedback.TooDim => "Got it. Keeping it clearer.",
            ComfortFeedback.TooWarm => "Got it. Less warm.",
            ComfortFeedback.TooCold => "Got it. A little warmer.",
            ComfortFeedback.Perfect => "Saved. Light Pilot will remember this.",
            _ => "Saved."
        };
        TransitionText = "Learning";
        OnPropertyChanged(nameof(ComfortIntensityText));
        OnPropertyChanged(nameof(BrightnessText));
        OnPropertyChanged(nameof(WarmthText));
    }

    private void SetIntensityPreset(int intensity)
    {
        ComfortIntensity = intensity;
    }

    private void UpdateSettings(UserSettings settings, bool persist)
    {
        _settings = settings;
        OnPropertyChanged(nameof(Settings));
        OnPropertyChanged(nameof(AutoEnabled));
        OnPropertyChanged(nameof(ComfortIntensity));

        if (persist)
        {
            _ = PersistSettingsAsync(settings, immediate: false);
        }

        UpdateTimerInterval();
        _ = ReloadMonitorsAsync();
        _ = RefreshDecisionAsync();
    }

    private void ShowHome()
    {
        SelectedSurface = MainSurface.Home;
    }

    private void ShowQuickAdjust()
    {
        SelectedSurface = MainSurface.QuickAdjust;
    }

    private void ShowSettingsSurface()
    {
        SettingsDraft = new SettingsViewModel(_settings, StartWithWindows);
        SelectedSurface = MainSurface.Settings;
    }

    private void SaveSettingsSurface()
    {
        if (SettingsDraft is null)
        {
            SelectedSurface = MainSurface.Home;
            return;
        }

        var startWithWindows = SettingsDraft.StartWithWindows;
        ApplySettings(SettingsDraft.ToSettings(_settings), startWithWindows);
        RequestStartupRegistrationChanged?.Invoke(this, startWithWindows);
        SelectedSurface = MainSurface.Home;
    }

    private async Task PersistSettingsAsync(UserSettings settings, bool immediate)
    {
        _settingsSaveCts?.Cancel();
        _settingsSaveCts?.Dispose();
        var cts = new CancellationTokenSource();
        _settingsSaveCts = cts;

        try
        {
            if (!immediate)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(750), cts.Token).ConfigureAwait(false);
            }

            await _settingsStore.SaveAsync(settings, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (IOException)
        {
            Reason = "Settings could not be saved";
        }
        catch (UnauthorizedAccessException)
        {
            Reason = "Settings could not be saved";
        }
    }

    private void UpdateTimerInterval()
    {
        var interval = RefreshCadencePolicy.GetInterval(_settings, _pauseUntil is not null, _settings.EnableContentBrightnessAnalysis);
        if (_timer.Interval != interval)
        {
            _timer.Interval = interval;
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

public enum MainSurface
{
    Home,
    QuickAdjust,
    Settings
}
