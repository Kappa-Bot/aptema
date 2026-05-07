using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices;
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
    private bool _startWithWindows;
    private bool _refreshInProgress;
    private readonly bool _hasInitialSettings;
    private CancellationTokenSource? _settingsSaveCts;

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
        SetCalmCommand = new RelayCommand(() => SetIntensityPreset(25));
        SetBalancedCommand = new RelayCommand(() => SetIntensityPreset(45));
        SetDeepComfortCommand = new RelayCommand(() => SetIntensityPreset(70));
        OpenSettingsCommand = new RelayCommand(() => RequestSettings?.Invoke(this, EventArgs.Empty));
        ExitCommand = new RelayCommand(() => RequestExit?.Invoke(this, EventArgs.Empty));

        _timer = new DispatcherTimer { Interval = RefreshCadencePolicy.GetInterval(_settings, isPaused: false, isContentAnalysisEnabled: false) };
        _timer.Tick += async (_, _) => await RefreshDecisionAsync().ConfigureAwait(true);

        _ = InitializeAsync();
    }

    public event EventHandler? RequestSettings;
    public event EventHandler? RequestExit;

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
    public RelayCommand SetCalmCommand { get; }
    public RelayCommand SetBalancedCommand { get; }
    public RelayCommand SetDeepComfortCommand { get; }
    public RelayCommand OpenSettingsCommand { get; }
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
            var content = effectiveSettings.AutoEnabled
                ? await SampleContentAsync(effectiveSettings.EnableContentBrightnessAnalysis).ConfigureAwait(true)
                : ContentLuminanceSample.Unknown;

            ComfortDecision? primaryDecision = null;
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

                await _brightnessController.ApplyAsync(monitor, decision, effectiveSettings, CancellationToken.None).ConfigureAwait(true);

                if (decision.ShouldApply)
                {
                    _engineStates[monitor.Id] = state with
                    {
                        LastAppliedAt = snapshot.Now,
                        LastDecision = decision.Target
                    };
                }

                UpdateMonitorStatus(index, monitor, decision, effectiveSettings);
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
            UpdateTimerInterval();
            OnPropertyChanged(nameof(BrightnessText));
            OnPropertyChanged(nameof(WarmthText));
            OnPropertyChanged(nameof(ComfortIntensityText));
            OnPropertyChanged(nameof(CurrentModeText));
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

    private void UpdateMonitorStatus(int index, MonitorModel monitor, ComfortDecision decision, UserSettings effectiveSettings)
    {
        var status = Monitors[index];
        if (decision.ShouldApply || status.BrightnessPercent == 0)
        {
            status.BrightnessPercent = decision.TargetBrightnessPercent;
            status.ColorTemperatureKelvin = monitor.SupportsColorTemperature ? decision.TargetColorTemperatureKelvin : 6500;
        }

        status.ControlLayer = GetControlLayer(monitor, effectiveSettings);
        status.Status = effectiveSettings.AutoEnabled && _pauseUntil is null ? "Ready" : "Paused";
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

    private static string GetControlLayer(MonitorModel monitor, UserSettings settings)
    {
        if (settings.EnableDdcCi && monitor.SupportsBrightnessControl)
        {
            return "DDC/CI";
        }

        return monitor.SupportsBrightnessControl ? "Windows brightness" : "Overlay fallback";
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
        var updated = ComfortPreferenceAdvisor.Apply(_settings, feedback);
        UpdateSettings(updated, persist: true);
        Reason = feedback == ComfortFeedback.TooBright ? "Got it. Making light softer." : "Got it. Keeping light clearer.";
        TransitionText = "Learning";
        OnPropertyChanged(nameof(ComfortIntensityText));
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
