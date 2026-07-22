using LightPilot.Core;

namespace LightPilot.Application;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
    DateTimeOffset LocalNow { get; }
}

public sealed class TimeProviderClock(TimeProvider timeProvider) : IClock
{
    public TimeProviderClock() : this(TimeProvider.System)
    {
    }

    public DateTimeOffset UtcNow => timeProvider.GetUtcNow();
    public DateTimeOffset LocalNow => timeProvider.GetLocalNow();
}

public interface ISettingsStore
{
    ValueTask<UserSettings> LoadAsync(CancellationToken cancellationToken);
    ValueTask SaveAsync(UserSettings settings, CancellationToken cancellationToken);
}

public interface IMonitorEnumerator
{
    ValueTask<IReadOnlyList<MonitorModel>> EnumerateAsync(CancellationToken cancellationToken);
}

public interface IDisplayTopologyObserver
{
    ValueTask UpdateTopologyAsync(IReadOnlyList<MonitorModel> displays, CancellationToken cancellationToken);
}

public interface IBrightnessController
{
    ValueTask<BrightnessApplyResult> ApplyAsync(MonitorModel monitor, ComfortDecision decision, UserSettings settings, CancellationToken cancellationToken);
}

public interface IUndoBrightnessController
{
    ValueTask<BrightnessApplyResult> ApplyUndoAsync(
        MonitorModel monitor,
        ComfortDecision decision,
        UserSettings settings,
        DateTimeOffset? retryAfter,
        CancellationToken cancellationToken);
}

public interface IForegroundWindowDetector
{
    AppContextModel Detect();
}

public interface IContentLuminanceSampler
{
    ValueTask<ContentLuminanceSample> SampleAsync(bool enabled, CancellationToken cancellationToken);
}

public interface IPowerStatusProvider
{
    bool IsOnBattery();
}

public interface IConfigurationTransferService
{
    ValueTask<ConfigurationImportPreview> PreviewImportAsync(string path, CancellationToken cancellationToken);
    ValueTask ExportAsync(string path, CancellationToken cancellationToken);
    ValueTask ImportAsync(string path, CancellationToken cancellationToken);
}

public interface IAdaptiveScheduler
{
    ValueTask DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
}

public sealed class TimeProviderAdaptiveScheduler(TimeProvider timeProvider) : IAdaptiveScheduler
{
    public TimeProviderAdaptiveScheduler() : this(TimeProvider.System)
    {
    }

    public ValueTask DelayAsync(TimeSpan delay, CancellationToken cancellationToken) =>
        new(Task.Delay(delay, timeProvider, cancellationToken));
}

public interface IComfortSession
{
    event Action<ComfortRuntimeSnapshot>? SnapshotChanged;

    UserSettings Settings { get; }
    ComfortRuntimeSnapshot CurrentSnapshot { get; }

    ValueTask StartAsync(UserSettings? initialSettings, CancellationToken cancellationToken);
    ValueTask StopAsync(CancellationToken cancellationToken);
    ValueTask RequestRefreshAsync(CancellationToken cancellationToken);
    ValueTask SetAutoEnabledAsync(bool enabled, CancellationToken cancellationToken);
    ValueTask PauseForAsync(TimeSpan duration, CancellationToken cancellationToken);
    ValueTask PauseUntilTomorrowAsync(CancellationToken cancellationToken);
    ValueTask ResumeAsync(CancellationToken cancellationToken);
    ValueTask ResetDefaultsAsync(CancellationToken cancellationToken);
    ValueTask ResetComfortAsync(CancellationToken cancellationToken);
    ValueTask ApplyFeedbackAsync(ComfortFeedback feedback, CancellationToken cancellationToken);
    ValueTask<OperationResult<ComfortRuntimeSnapshot>> UndoFeedbackAsync(CancellationToken cancellationToken);
    ValueTask ApplySettingsAsync(UserSettings settings, CancellationToken cancellationToken);
}
