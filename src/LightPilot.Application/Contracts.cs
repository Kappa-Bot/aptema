using System.Collections.Immutable;
using LightPilot.Core;

namespace LightPilot.Application;

public enum OperationStatus
{
    Success,
    ValidationFailure,
    Conflict,
    Unavailable,
    Degraded,
    Failed
}

public sealed record OperationResult<T>(OperationStatus Status, T? Value, string? Code = null)
{
    public bool IsUsable => Status is OperationStatus.Success or OperationStatus.Degraded && Value is not null;

    public static OperationResult<T> Succeeded(T value) => new(OperationStatus.Success, value);

    public static OperationResult<T> Degraded(T value, string code) => new(OperationStatus.Degraded, value, code);

    public static OperationResult<T> Failure(OperationStatus status, string code) => new(status, default, code);
}

public sealed record DisplayRuntimeState(
    MonitorModel Monitor,
    ComfortDecision Decision,
    BrightnessApplyResult ApplyResult);

public sealed record LearningSummary(int ContextCount, int SampleCount, double HighestConfidence)
{
    public static LearningSummary Empty { get; } = new(0, 0, 0);

    public static LearningSummary From(UserSettings settings)
    {
        var aggregates = settings.PreferenceLearning.Aggregates;
        return new LearningSummary(
            aggregates.Count,
            aggregates.Sum(item => item.Samples),
            aggregates.Count == 0 ? 0 : aggregates.Max(item => item.Confidence));
    }
}

public sealed record SystemHealthState
{
    public SystemHealthState(bool isDegraded, IEnumerable<string> issues)
    {
        IsDegraded = isDegraded;
        Issues = issues.ToImmutableArray();
    }

    public bool IsDegraded { get; init; }
    public ImmutableArray<string> Issues { get; init; }

    public static SystemHealthState Healthy { get; } = new(false, []);
}

public sealed record ComfortRuntimeSnapshot
{
    public ComfortRuntimeSnapshot(
        DateTimeOffset capturedAt,
        AppContextModel appContext,
        ContentLuminanceSample content,
        IEnumerable<DisplayRuntimeState> displays,
        ComfortDecision? primaryDecision,
        DateTimeOffset? pausedUntil,
        LearningSummary learning,
        SystemHealthState health)
    {
        CapturedAt = capturedAt;
        AppContext = appContext;
        Content = content;
        Displays = displays.ToImmutableArray();
        PrimaryDecision = primaryDecision;
        PausedUntil = pausedUntil;
        Learning = learning;
        Health = health;
    }

    public DateTimeOffset CapturedAt { get; init; }
    public AppContextModel AppContext { get; init; }
    public ContentLuminanceSample Content { get; init; }
    public ImmutableArray<DisplayRuntimeState> Displays { get; init; }
    public ComfortDecision? PrimaryDecision { get; init; }
    public DateTimeOffset? PausedUntil { get; init; }
    public LearningSummary Learning { get; init; }
    public SystemHealthState Health { get; init; }

    public static ComfortRuntimeSnapshot Empty(DateTimeOffset capturedAt) => new(
        capturedAt,
        new AppContextModel("unknown.exe", AppCategory.Unknown, false),
        ContentLuminanceSample.Unknown,
        Array.Empty<DisplayRuntimeState>(),
        null,
        null,
        LearningSummary.Empty,
        SystemHealthState.Healthy);
}

public sealed record ComfortContextUpdate(
    DateTimeOffset CapturedAt,
    AppContextModel AppContext,
    ContentLuminanceSample Content);

public sealed record ComfortRefreshRequest(
    UserSettings Settings,
    IReadOnlyList<MonitorModel> Displays,
    ComfortRuntimeSnapshot? Previous,
    DateTimeOffset? PauseUntil,
    TimeSpan ScreenSessionLength);

public sealed record FeedbackRequest(
    ComfortFeedback Feedback,
    UserSettings Settings,
    IReadOnlyList<MonitorModel> Displays,
    AppContextModel AppContext,
    ContentLuminanceSample Content,
    int CurrentBrightness,
    int CurrentColorTemperatureKelvin,
    TimeSpan ScreenSessionLength);

public sealed record FeedbackOutcome
{
    public FeedbackOutcome(UserSettings settings, ComfortDecision decision, IEnumerable<BrightnessApplyResult> applyResults)
    {
        Settings = settings;
        Decision = decision;
        ApplyResults = applyResults.ToImmutableArray();
    }

    public UserSettings Settings { get; init; }
    public ComfortDecision Decision { get; init; }
    public ImmutableArray<BrightnessApplyResult> ApplyResults { get; init; }
}

public sealed record ConfigurationImportPreview(
    bool IsValid,
    int SchemaVersion,
    string Summary,
    IReadOnlyList<string> Warnings);
