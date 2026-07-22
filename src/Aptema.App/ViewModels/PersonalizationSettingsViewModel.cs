using System.Collections.ObjectModel;
using System.IO;
using Aptema.Core;

namespace Aptema.App.ViewModels;

public sealed class PersonalizationSettingsViewModel : ObservableObject
{
    private readonly string? _currentProcessName;
    private AppCategory _selectedApplicationCategory;
    private string _newProfileName = string.Empty;
    private string _newAutomationName = string.Empty;
    private DayPhase? _newAutomationPhase = DayPhase.Night;
    private int _newAutomationBrightnessOffset = -3;
    private int _newAutomationWarmthOffset = -140;
    private bool _resetLearning;

    public PersonalizationSettingsViewModel(UserSettings settings, string? currentProcessName, AppCategory currentCategory)
    {
        _currentProcessName = NormalizeProcess(currentProcessName);
        _selectedApplicationCategory = currentCategory;
        ApplicationRules = new ObservableCollection<ApplicationComfortRule>(settings.ApplicationRules ?? []);
        CustomProfiles = new ObservableCollection<CustomComfortProfile>(settings.CustomProfiles ?? []);
        AutomationRules = new ObservableCollection<ComfortAutomationRule>(settings.AutomationRules ?? []);
        AppCategories = Enum.GetValues<AppCategory>().Select(category => new AppCategoryOption(category, CategoryLabel(category))).ToArray();
        DayPhases = Enum.GetValues<DayPhase>().Select(phase => new DayPhaseOption(phase, phase.ToString())).ToArray();

        SaveCurrentApplicationRuleCommand = new RelayCommand(_ => SaveCurrentApplicationRule(), _ => HasCurrentApplication);
        CreateProfileCommand = new RelayCommand(_ => CreateProfile(), _ => !string.IsNullOrWhiteSpace(NewProfileName));
        RemoveProfileCommand = new RelayCommand(value => RemoveProfile(value?.ToString()));
        CreateAutomationRuleCommand = new RelayCommand(_ => CreateAutomationRule(), _ => !string.IsNullOrWhiteSpace(NewAutomationName));
        RemoveAutomationRuleCommand = new RelayCommand(value => RemoveAutomationRule(value?.ToString()));
        ResetLearningCommand = new RelayCommand(ResetLearning);
    }

    public ObservableCollection<ApplicationComfortRule> ApplicationRules { get; }
    public ObservableCollection<CustomComfortProfile> CustomProfiles { get; }
    public ObservableCollection<ComfortAutomationRule> AutomationRules { get; }
    public IReadOnlyList<AppCategoryOption> AppCategories { get; }
    public IReadOnlyList<DayPhaseOption> DayPhases { get; }
    public RelayCommand SaveCurrentApplicationRuleCommand { get; }
    public RelayCommand CreateProfileCommand { get; }
    public RelayCommand RemoveProfileCommand { get; }
    public RelayCommand CreateAutomationRuleCommand { get; }
    public RelayCommand RemoveAutomationRuleCommand { get; }
    public RelayCommand ResetLearningCommand { get; }

    public bool HasCurrentApplication => !string.IsNullOrWhiteSpace(_currentProcessName);
    public string CurrentApplicationName => HasCurrentApplication
        ? Path.GetFileNameWithoutExtension(_currentProcessName) ?? "Current application"
        : "No application detected";
    public int LearningSignalCount { get; private set; }

    public AppCategory SelectedApplicationCategory
    {
        get => _selectedApplicationCategory;
        set => SetProperty(ref _selectedApplicationCategory, value);
    }

    public string NewProfileName
    {
        get => _newProfileName;
        set
        {
            if (SetProperty(ref _newProfileName, value)) CreateProfileCommand.RaiseCanExecuteChanged();
        }
    }

    public string NewAutomationName
    {
        get => _newAutomationName;
        set
        {
            if (SetProperty(ref _newAutomationName, value)) CreateAutomationRuleCommand.RaiseCanExecuteChanged();
        }
    }

    public DayPhase? NewAutomationPhase
    {
        get => _newAutomationPhase;
        set => SetProperty(ref _newAutomationPhase, value);
    }

    public int NewAutomationBrightnessOffset
    {
        get => _newAutomationBrightnessOffset;
        set => SetProperty(ref _newAutomationBrightnessOffset, Math.Clamp(value, -12, 12));
    }

    public int NewAutomationWarmthOffset
    {
        get => _newAutomationWarmthOffset;
        set => SetProperty(ref _newAutomationWarmthOffset, Math.Clamp(value, -480, 480));
    }

    public void SaveCurrentApplicationRule()
    {
        if (!HasCurrentApplication) return;
        var existing = ApplicationRules.FirstOrDefault(rule =>
            string.Equals(rule.ProcessName, _currentProcessName, StringComparison.OrdinalIgnoreCase));
        var rule = new ApplicationComfortRule(
            existing?.Id ?? NewId("app"),
            $"{CurrentApplicationName} comfort",
            true,
            existing?.Priority ?? 10,
            _currentProcessName!,
            SelectedApplicationCategory,
            existing?.Profile,
            existing?.CustomProfileId,
            existing?.IntensityOffset ?? 0,
            existing?.ProtectFullscreen ?? true);
        if (existing is not null) ApplicationRules.Remove(existing);
        ApplicationRules.Add(rule);
    }

    public void CreateProfile()
    {
        var name = NewProfileName.Trim();
        if (name.Length == 0) return;
        CustomProfiles.Add(new CustomComfortProfile(NewId("profile"), name, 72, 58, 46, 6200, 4600, 3700, 110, true));
        NewProfileName = string.Empty;
    }

    public void RemoveProfile(string? id)
    {
        var profile = CustomProfiles.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));
        if (profile is null) return;
        CustomProfiles.Remove(profile);
        foreach (var rule in ApplicationRules.Where(item => string.Equals(item.CustomProfileId, profile.Id, StringComparison.OrdinalIgnoreCase)).ToArray())
        {
            ApplicationRules.Remove(rule);
            ApplicationRules.Add(rule with { CustomProfileId = null });
        }
    }

    public void CreateAutomationRule()
    {
        var name = NewAutomationName.Trim();
        if (name.Length == 0) return;
        AutomationRules.Add(new ComfortAutomationRule(
            NewId("automation"), name, true, 10, NewAutomationPhase, null, false, null,
            Math.Clamp(NewAutomationBrightnessOffset, -12, 12),
            Math.Clamp(NewAutomationWarmthOffset, -480, 480)));
        NewAutomationName = string.Empty;
    }

    public void RemoveAutomationRule(string? id)
    {
        var rule = AutomationRules.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));
        if (rule is not null) AutomationRules.Remove(rule);
    }

    public void ResetLearning()
    {
        _resetLearning = true;
        LearningSignalCount = 0;
        OnPropertyChanged(nameof(LearningSignalCount));
    }

    public UserSettings ToSettings(UserSettings current) => current with
    {
        ApplicationRules = ApplicationRules.ToArray(),
        CustomProfiles = CustomProfiles.ToArray(),
        AutomationRules = AutomationRules.ToArray(),
        AppOverrides = ApplicationRules.ToDictionary(rule => rule.ProcessName, rule => rule.Category, StringComparer.OrdinalIgnoreCase),
        PreferenceLearning = _resetLearning ? PreferenceLearningModel.Empty : current.PreferenceLearning
    };

    public void LoadLearningCount(UserSettings settings)
    {
        LearningSignalCount = settings.PreferenceLearning?.Aggregates.Sum(item => item.Samples) ?? 0;
        OnPropertyChanged(nameof(LearningSignalCount));
    }

    private static string? NormalizeProcess(string? processName) => string.IsNullOrWhiteSpace(processName)
        ? null
        : Path.GetFileName(processName.Trim());

    private static string NewId(string prefix) => $"{prefix}:{Guid.NewGuid():N}";

    private static string CategoryLabel(AppCategory category) => category switch
    {
        AppCategory.EmailCommunication => "Email and communication",
        AppCategory.VideoMedia => "Video",
        AppCategory.MusicAudio => "Music and audio",
        AppCategory.OfficeReading => "Office and reading",
        _ => category.ToString()
    };
}

public sealed record AppCategoryOption(AppCategory Value, string Label);
public sealed record DayPhaseOption(DayPhase Value, string Label);
