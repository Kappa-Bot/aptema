namespace LightPilot.Core;

public enum AppCategory
{
    Unknown,
    Browser,
    EmailCommunication,
    Development,
    Gaming,
    VideoMedia,
    MusicAudio,
    OfficeReading,
    Creative,
    System
}

public enum ComfortProfileId
{
    Auto,
    Day,
    Evening,
    Night,
    Reading,
    Gaming,
    Video,
    Development,
    Manual,
    Paused
}

public enum LuminanceClassification
{
    Unknown,
    Dark,
    Neutral,
    Balanced,
    Bright,
    MostlyWhite,
    HighContrast
}

public enum DayPhase
{
    Day,
    Evening,
    Night
}

public enum ComfortFeedback
{
    TooBright,
    TooDim,
    TooWarm,
    TooCold,
    Perfect
}

public enum DecisionSource
{
    Default,
    Learned,
    Manual,
    Protected,
    Paused
}

public enum BrightnessControlLayer
{
    None,
    DdcCi,
    WindowsBrightness,
    Overlay,
    Gamma
}

public enum MonitorControlState
{
    Ready,
    NoChange,
    Throttled,
    FallbackUsed,
    Degraded,
    Unsupported,
    Disabled,
    Protected,
    Failed
}

public readonly record struct DisplayBounds(int X, int Y, int Width, int Height)
{
    public bool IsValid => Width > 0 && Height > 0;
}

public sealed record BrightnessApplyResult(
    string MonitorId,
    BrightnessControlLayer PreferredLayer,
    BrightnessControlLayer AppliedLayer,
    MonitorControlState State,
    string ReasonCode,
    DateTimeOffset? SuppressedUntil = null,
    bool UsedHardware = false,
    bool UsedOverlay = false)
{
    public static BrightnessApplyResult NoChange(string monitorId, string reasonCode = "NoChange")
    {
        return new BrightnessApplyResult(monitorId, BrightnessControlLayer.None, BrightnessControlLayer.None, MonitorControlState.NoChange, reasonCode);
    }
}

public sealed record MonitorModel(
    string Id,
    string Name,
    bool SupportsBrightnessControl,
    bool SupportsColorTemperature,
    int MinimumBrightnessPercent,
    int MaximumBrightnessPercent,
    int BrightnessOffsetPercent,
    long NativeHandle = 0,
    DisplayBounds Bounds = default,
    DisplayBounds WorkArea = default,
    bool IsPrimary = false,
    IReadOnlyList<string>? LegacyAliases = null,
    string? HumanDisplayName = null)
{
    public IReadOnlyList<string> Aliases => LegacyAliases ?? Array.Empty<string>();
    public string DisplayName => string.IsNullOrWhiteSpace(HumanDisplayName) ? Name : HumanDisplayName;
}

public sealed record DisplayConfiguration(
    string StableId,
    IReadOnlyList<string> LegacyAliases,
    bool IsEnabled,
    int BrightnessOffsetPercent,
    int MinimumBrightnessPercent,
    int MaximumBrightnessPercent,
    bool AllowSoftwareFallback);

public sealed record HotkeyConfiguration(
    string? QuickAdjust,
    string? TooBright,
    string? TooDim,
    string? Warmer,
    string? Cooler,
    string? PauseResume)
{
    public static HotkeyConfiguration Default { get; } = new("Win+Alt+A", null, null, null, null, null);
}

public sealed record MonitorPreference
{
    public string MonitorId { get; init; } = "";
    public int BrightnessOffsetPercent { get; init; }
    public int? MinimumBrightnessPercent { get; init; }
    public int? MaximumBrightnessPercent { get; init; }
    public bool UseSoftwareFallback { get; init; }
    public bool IsDisabled { get; init; }
}

public sealed record AppContextModel
{
    public AppContextModel(string? processName, AppCategory category, bool isFullscreen, bool isPresentation = false)
    {
        ProcessName = processName;
        Category = category;
        IsFullscreen = isFullscreen;
        IsPresentation = isPresentation;
    }

    public string? ProcessName { get; init; }
    public AppCategory Category { get; init; }
    public bool IsFullscreen { get; init; }
    public bool IsPresentation { get; init; }
    public bool IsProtected => IsPresentation || Category is AppCategory.Gaming || (IsFullscreen && Category is AppCategory.VideoMedia);
}

public sealed record ContentLuminanceSample(
    bool IsEnabled,
    double AverageLuminance,
    double WhitePixelRatio,
    double BrightPixelRatio,
    double DarkPixelRatio,
    LuminanceClassification Classification)
{
    public static ContentLuminanceSample Unknown { get; } = new(false, 0, 0, 0, 0, LuminanceClassification.Unknown);
}

public sealed record LightTarget(int BrightnessPercent, int ColorTemperatureKelvin, double OverlayOpacity);

public sealed record ComfortDecision(
    ComfortProfileId Profile,
    int TargetBrightnessPercent,
    int TargetColorTemperatureKelvin,
    double OverlayOpacity,
    TimeSpan TransitionDuration,
    bool ShouldApply,
    string Reason,
    IReadOnlyList<string> ReasonCodes,
    double Confidence = 0.65,
    DecisionSource Source = DecisionSource.Default,
    bool IsLearned = false)
{
    public LightTarget Target => new(TargetBrightnessPercent, TargetColorTemperatureKelvin, OverlayOpacity);
}

public sealed record AdaptiveSnapshot(
    DateTimeOffset Now,
    MonitorModel Monitor,
    AppContextModel AppContext,
    ContentLuminanceSample Content,
    TimeSpan ScreenTimeSessionLength,
    int CurrentBrightness,
    int CurrentColorTemperatureKelvin,
    DateTimeOffset? ManualOverrideUntil);

public sealed record AdaptiveEngineState
{
    public static AdaptiveEngineState Empty { get; } = new();

    public DateTimeOffset? LastAppliedAt { get; init; }
    public LightTarget? LastDecision { get; init; }
    public DateTimeOffset? ProtectedUntil { get; init; }
}

public sealed record PreferenceLearningContext(
    string MonitorId,
    AppCategory AppCategory,
    DayPhase DayPhase,
    bool IsFullscreen,
    LuminanceClassification Luminance)
{
    public string Key => string.Join('|',
        Normalize(MonitorId),
        AppCategory,
        DayPhase,
        IsFullscreen ? "fullscreen" : "windowed",
        Luminance);

    public static PreferenceLearningContext FromSnapshot(AdaptiveSnapshot snapshot)
    {
        return new PreferenceLearningContext(
            snapshot.Monitor.Id,
            snapshot.AppContext.Category,
            DayPhasePolicy.GetPhase(snapshot.Now),
            snapshot.AppContext.IsFullscreen,
            snapshot.Content.Classification);
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim().ToUpperInvariant();
    }
}

public sealed record PreferenceAdjustment(
    int BrightnessOffsetPercent,
    int WarmthOffsetKelvin,
    double Confidence,
    bool IsLearned)
{
    public static PreferenceAdjustment None { get; } = new(0, 0, 0, false);
}

public sealed record PreferenceCorrectionAggregate
{
    public string Key { get; init; } = "";
    public string MonitorId { get; init; } = "";
    public AppCategory AppCategory { get; init; } = AppCategory.Unknown;
    public DayPhase DayPhase { get; init; } = DayPhase.Day;
    public bool IsFullscreen { get; init; }
    public LuminanceClassification Luminance { get; init; } = LuminanceClassification.Unknown;
    public int Samples { get; init; }
    public int PerfectCount { get; init; }
    public int NetBrightnessScore { get; init; }
    public int NetWarmthScore { get; init; }
    public int BrightnessOffsetPercent { get; init; }
    public int WarmthOffsetKelvin { get; init; }
    public double Confidence { get; init; }
    public DateTimeOffset LastUpdatedAt { get; init; }
}

public sealed record PreferenceLearningModel
{
    public static PreferenceLearningModel Empty { get; } = new();

    public IReadOnlyList<PreferenceCorrectionAggregate> Aggregates { get; init; } = Array.Empty<PreferenceCorrectionAggregate>();
}

public sealed record UserSettings
{
    public static UserSettings Default { get; } = new();

    public int SchemaVersion { get; init; } = 5;
    public bool AutoEnabled { get; init; } = true;
    public int ComfortIntensity { get; init; } = 45;
    public TimeOnly WakeTime { get; init; } = new(7, 0);
    public TimeOnly SleepTime { get; init; } = new(23, 0);
    public int MinimumBrightnessPercent { get; init; } = 25;
    public int MaximumBrightnessPercent { get; init; } = 90;
    public bool EnableDdcCi { get; init; } = true;
    public bool EnableContentBrightnessAnalysis { get; init; } = false;
    public bool EnablePreferenceLearning { get; init; } = true;
    public bool GamingVideoProtection { get; init; } = true;
    public bool ReduceWorkOnBattery { get; init; } = true;
    public bool HasCompletedOnboarding { get; init; } = false;
    public TimeSpan TransitionSpeed { get; init; } = TimeSpan.FromSeconds(90);
    public PreferenceLearningModel PreferenceLearning { get; init; } = PreferenceLearningModel.Empty;
    public IReadOnlyDictionary<string, AppCategory> AppOverrides { get; init; } = new Dictionary<string, AppCategory>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<MonitorPreference> MonitorPreferences { get; init; } = Array.Empty<MonitorPreference>();
    public IReadOnlyList<DisplayConfiguration> DisplayConfigurations { get; init; } = Array.Empty<DisplayConfiguration>();
    public HotkeyConfiguration Hotkeys { get; init; } = HotkeyConfiguration.Default;
}
