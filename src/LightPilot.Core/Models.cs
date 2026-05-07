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
    Bright,
    MostlyWhite
}

public enum BrightnessControlLayer
{
    None,
    DdcCi,
    WindowsBrightness,
    Overlay,
    Gamma
}

public sealed record MonitorModel(
    string Id,
    string Name,
    bool SupportsBrightnessControl,
    bool SupportsColorTemperature,
    int MinimumBrightnessPercent,
    int MaximumBrightnessPercent,
    int BrightnessOffsetPercent,
    long NativeHandle = 0);

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
    IReadOnlyList<string> ReasonCodes)
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

public sealed record UserSettings
{
    public static UserSettings Default { get; } = new();

    public int SchemaVersion { get; init; } = 2;
    public bool AutoEnabled { get; init; } = true;
    public int ComfortIntensity { get; init; } = 45;
    public TimeOnly WakeTime { get; init; } = new(7, 0);
    public TimeOnly SleepTime { get; init; } = new(23, 0);
    public int MinimumBrightnessPercent { get; init; } = 25;
    public int MaximumBrightnessPercent { get; init; } = 90;
    public bool EnableDdcCi { get; init; } = true;
    public bool EnableContentBrightnessAnalysis { get; init; } = false;
    public bool GamingVideoProtection { get; init; } = true;
    public bool ReduceWorkOnBattery { get; init; } = true;
    public bool HasCompletedOnboarding { get; init; } = false;
    public TimeSpan TransitionSpeed { get; init; } = TimeSpan.FromSeconds(90);
    public IReadOnlyDictionary<string, AppCategory> AppOverrides { get; init; } = new Dictionary<string, AppCategory>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<MonitorPreference> MonitorPreferences { get; init; } = Array.Empty<MonitorPreference>();
}
