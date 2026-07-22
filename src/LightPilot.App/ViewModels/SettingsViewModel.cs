using LightPilot.Core;

namespace LightPilot.App.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private bool _startWithWindows;
    private int _comfortIntensity;
    private string _wakeTime = "";
    private string _sleepTime = "";
    private int _minimumBrightnessPercent;
    private int _maximumBrightnessPercent;
    private bool _enableDdcCi;
    private bool _enableContentBrightnessAnalysis;
    private bool _enablePreferenceLearning;
    private bool _gamingVideoProtection;
    private bool _reduceWorkOnBattery;
    private bool _safeModeEnabled;
    private int _transitionSpeedSeconds;
    private string _appOverridesText = "";
    private string _monitorPreferencesText = "";
    private bool _resetLearningRequested;

    public SettingsViewModel(UserSettings settings, bool startWithWindows)
    {
        Load(settings, startWithWindows);
        ResetCommand = new RelayCommand(Reset);
        ResetLearningCommand = new RelayCommand(ResetLearning);
    }

    public RelayCommand ResetCommand { get; }

    public RelayCommand ResetLearningCommand { get; }

    public bool StartWithWindows
    {
        get => _startWithWindows;
        set => SetProperty(ref _startWithWindows, value);
    }

    public int ComfortIntensity
    {
        get => _comfortIntensity;
        set
        {
            if (SetProperty(ref _comfortIntensity, value))
            {
                OnPropertyChanged(nameof(ComfortIntensityText));
            }
        }
    }

    public string ComfortIntensityText => ComfortCopy.DescribeIntensity(ComfortIntensity);

    public string WakeTime
    {
        get => _wakeTime;
        set => SetProperty(ref _wakeTime, value);
    }

    public string SleepTime
    {
        get => _sleepTime;
        set => SetProperty(ref _sleepTime, value);
    }

    public int MinimumBrightnessPercent
    {
        get => _minimumBrightnessPercent;
        set
        {
            if (SetProperty(ref _minimumBrightnessPercent, value) && _maximumBrightnessPercent < value)
            {
                MaximumBrightnessPercent = value;
            }
        }
    }

    public int MaximumBrightnessPercent
    {
        get => _maximumBrightnessPercent;
        set
        {
            if (SetProperty(ref _maximumBrightnessPercent, value) && _minimumBrightnessPercent > value)
            {
                MinimumBrightnessPercent = value;
            }
        }
    }

    public bool EnableDdcCi
    {
        get => _enableDdcCi;
        set => SetProperty(ref _enableDdcCi, value);
    }

    public bool EnableContentBrightnessAnalysis
    {
        get => _enableContentBrightnessAnalysis;
        set => SetProperty(ref _enableContentBrightnessAnalysis, value);
    }

    public bool EnablePreferenceLearning
    {
        get => _enablePreferenceLearning;
        set => SetProperty(ref _enablePreferenceLearning, value);
    }

    public bool GamingVideoProtection
    {
        get => _gamingVideoProtection;
        set => SetProperty(ref _gamingVideoProtection, value);
    }

    public bool ReduceWorkOnBattery
    {
        get => _reduceWorkOnBattery;
        set => SetProperty(ref _reduceWorkOnBattery, value);
    }

    public bool SafeModeEnabled
    {
        get => _safeModeEnabled;
        set => SetProperty(ref _safeModeEnabled, value);
    }

    public int TransitionSpeedSeconds
    {
        get => _transitionSpeedSeconds;
        set => SetProperty(ref _transitionSpeedSeconds, value);
    }

    public string AppOverridesText
    {
        get => _appOverridesText;
        set => SetProperty(ref _appOverridesText, value);
    }

    public string MonitorPreferencesText
    {
        get => _monitorPreferencesText;
        set => SetProperty(ref _monitorPreferencesText, value);
    }

    public UserSettings ToSettings(UserSettings current)
    {
        var wakeTime = ParseTime(WakeTime, current.WakeTime);
        var sleepTime = ParseTime(SleepTime, current.SleepTime);
        var minimum = Math.Clamp(MinimumBrightnessPercent, 15, 100);
        var maximum = Math.Clamp(MaximumBrightnessPercent, minimum, 100);

        return current with
        {
            ComfortIntensity = Math.Clamp(ComfortIntensity, 0, 100),
            WakeTime = wakeTime,
            SleepTime = sleepTime,
            MinimumBrightnessPercent = minimum,
            MaximumBrightnessPercent = maximum,
            EnableDdcCi = EnableDdcCi,
            EnableContentBrightnessAnalysis = EnableContentBrightnessAnalysis,
            EnablePreferenceLearning = EnablePreferenceLearning,
            GamingVideoProtection = GamingVideoProtection,
            ReduceWorkOnBattery = ReduceWorkOnBattery,
            SafeModeEnabled = SafeModeEnabled,
            TransitionSpeed = TimeSpan.FromSeconds(Math.Clamp(TransitionSpeedSeconds, 30, 240)),
            PreferenceLearning = _resetLearningRequested ? PreferenceLearningModel.Empty : current.PreferenceLearning,
            AppOverrides = AppOverrideTextCodec.Parse(AppOverridesText),
            MonitorPreferences = MonitorPreferenceTextCodec.Parse(MonitorPreferencesText)
        };
    }

    private void Load(UserSettings settings, bool startWithWindows)
    {
        _resetLearningRequested = false;
        StartWithWindows = startWithWindows;
        ComfortIntensity = settings.ComfortIntensity;
        WakeTime = settings.WakeTime.ToString("HH:mm");
        SleepTime = settings.SleepTime.ToString("HH:mm");
        MinimumBrightnessPercent = settings.MinimumBrightnessPercent;
        MaximumBrightnessPercent = settings.MaximumBrightnessPercent;
        EnableDdcCi = settings.EnableDdcCi;
        EnableContentBrightnessAnalysis = settings.EnableContentBrightnessAnalysis;
        EnablePreferenceLearning = settings.EnablePreferenceLearning;
        GamingVideoProtection = settings.GamingVideoProtection;
        ReduceWorkOnBattery = settings.ReduceWorkOnBattery;
        SafeModeEnabled = settings.SafeModeEnabled;
        TransitionSpeedSeconds = (int)Math.Round(settings.TransitionSpeed.TotalSeconds);
        AppOverridesText = AppOverrideTextCodec.Format(settings.AppOverrides);
        MonitorPreferencesText = MonitorPreferenceTextCodec.Format(settings.MonitorPreferences);
    }

    private void Reset()
    {
        Load(UserSettings.Default, false);
    }

    private void ResetLearning()
    {
        EnablePreferenceLearning = true;
        _resetLearningRequested = true;
    }

    private static TimeOnly ParseTime(string value, TimeOnly fallback)
    {
        return TimeOnly.TryParse(value, out var parsed) ? parsed : fallback;
    }
}
