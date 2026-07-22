using System.Collections.ObjectModel;
using Aptema.Core;

namespace Aptema.App.ViewModels;

public enum OnboardingStep { Welcome, Displays, MethodCheck, Calibration, Schedule, Finish }

public sealed class OnboardingViewModel : ObservableObject
{
    private OnboardingStep _currentStep;
    private bool _privacyAccepted;
    private int _comfortIntensity;
    private string _wakeTime;
    private string _sleepTime;
    private bool _fullscreenProtection;
    private bool _startWithWindows;

    public OnboardingViewModel(UserSettings settings, IReadOnlyList<MonitorModel> displays, bool startWithWindows)
    {
        Displays = new ObservableCollection<OnboardingDisplayViewModel>(displays.Select((display, index) => new OnboardingDisplayViewModel(index + 1, display)));
        SelectedDisplay = Displays.FirstOrDefault();
        _comfortIntensity = settings.ComfortIntensity;
        _wakeTime = settings.WakeTime.ToString("HH:mm");
        _sleepTime = settings.SleepTime.ToString("HH:mm");
        _fullscreenProtection = settings.GamingVideoProtection;
        _startWithWindows = startWithWindows;
        BackCommand = new RelayCommand(MoveBack);
        NextCommand = new RelayCommand(MoveNext);
        SkipCommand = new RelayCommand(MoveNext);
        IdentifySelectedDisplayCommand = new RelayCommand(() => Raise(IdentifyDisplayRequested));
        TestSelectedDisplayCommand = new RelayCommand(() => Raise(DisplayTestRequested));
    }

    public event EventHandler<MonitorModel>? IdentifyDisplayRequested;
    public event EventHandler<MonitorModel>? DisplayTestRequested;
    public ObservableCollection<OnboardingDisplayViewModel> Displays { get; }
    public OnboardingDisplayViewModel? SelectedDisplay { get; set; }
    public RelayCommand BackCommand { get; }
    public RelayCommand NextCommand { get; }
    public RelayCommand SkipCommand { get; }
    public RelayCommand IdentifySelectedDisplayCommand { get; }
    public RelayCommand TestSelectedDisplayCommand { get; }

    public OnboardingStep CurrentStep { get => _currentStep; private set { if (SetProperty(ref _currentStep, value)) Changed(); } }
    public bool PrivacyAccepted { get => _privacyAccepted; set { if (SetProperty(ref _privacyAccepted, value)) OnPropertyChanged(nameof(CanMoveNext)); } }
    public bool CanMoveNext => CurrentStep switch
    {
        OnboardingStep.Welcome => PrivacyAccepted,
        OnboardingStep.Schedule => TimeOnly.TryParse(WakeTime, out _) && TimeOnly.TryParse(SleepTime, out _),
        _ => true
    };
    public bool CanMoveBack => CurrentStep != OnboardingStep.Welcome;
    public bool IsLastStep => CurrentStep == OnboardingStep.Finish;
    public int StepNumber => (int)CurrentStep + 1;
    public int ComfortIntensity { get => _comfortIntensity; set => SetProperty(ref _comfortIntensity, Math.Clamp(value, 0, 100)); }
    public string WakeTime { get => _wakeTime; set { if (SetProperty(ref _wakeTime, value)) OnPropertyChanged(nameof(CanMoveNext)); } }
    public string SleepTime { get => _sleepTime; set { if (SetProperty(ref _sleepTime, value)) OnPropertyChanged(nameof(CanMoveNext)); } }
    public bool FullscreenProtection { get => _fullscreenProtection; set => SetProperty(ref _fullscreenProtection, value); }
    public bool StartWithWindows { get => _startWithWindows; set => SetProperty(ref _startWithWindows, value); }

    public void MoveNext()
    {
        if (!CanMoveNext || IsLastStep) return;
        CurrentStep++;
    }

    public void MoveBack()
    {
        if (CanMoveBack) CurrentStep--;
    }

    public UserSettings Complete(UserSettings current) => current with
    {
        HasCompletedOnboarding = true,
        ComfortIntensity = ComfortIntensity,
        WakeTime = TimeOnly.TryParse(WakeTime, out var wake) ? wake : current.WakeTime,
        SleepTime = TimeOnly.TryParse(SleepTime, out var sleep) ? sleep : current.SleepTime,
        GamingVideoProtection = FullscreenProtection,
        DisplayConfigurations = Displays.Select(display => new DisplayConfiguration(
            display.Monitor.Id, display.Monitor.Aliases.ToArray(), true, 0,
            current.MinimumBrightnessPercent, current.MaximumBrightnessPercent, true)).ToArray()
    };

    private void Raise(EventHandler<MonitorModel>? handler)
    {
        if (SelectedDisplay is not null) handler?.Invoke(this, SelectedDisplay.Monitor);
    }

    private void Changed()
    {
        OnPropertyChanged(nameof(CanMoveNext)); OnPropertyChanged(nameof(CanMoveBack));
        OnPropertyChanged(nameof(IsLastStep)); OnPropertyChanged(nameof(StepNumber));
    }
}

public sealed record OnboardingDisplayViewModel(int Number, MonitorModel Monitor)
{
    public string Label => $"{Number}. {Monitor.DisplayName}";
}
