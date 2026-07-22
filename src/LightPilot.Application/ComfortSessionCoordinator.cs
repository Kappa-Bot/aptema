using LightPilot.Core;

namespace LightPilot.Application;

public sealed class ComfortSessionCoordinator(
    ConfigurationCoordinator configurationCoordinator,
    DisplayLifecycleCoordinator displayLifecycleCoordinator,
    ComfortAutomationCoordinator automationCoordinator,
    FeedbackCoordinator feedbackCoordinator,
    IBrightnessController brightnessController,
    IClock clock,
    IAdaptiveScheduler scheduler) : IComfortSession
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IReadOnlyList<MonitorModel> _displays = Array.Empty<MonitorModel>();
    private CancellationTokenSource? _loopCancellation;
    private Task? _loopTask;
    private DateTimeOffset _sessionStartedAt = clock.LocalNow;
    private DateTimeOffset? _pauseUntil;

    public event Action<ComfortRuntimeSnapshot>? SnapshotChanged;

    public UserSettings Settings { get; private set; } = UserSettings.Default;
    public ComfortRuntimeSnapshot CurrentSnapshot { get; private set; } = ComfortRuntimeSnapshot.Empty(clock.UtcNow);

    public async ValueTask StartAsync(UserSettings? initialSettings, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_loopCancellation is not null)
            {
                return;
            }

            if (initialSettings is not null)
            {
                Settings = initialSettings;
            }
            else
            {
                var loaded = await configurationCoordinator.LoadAsync(cancellationToken).ConfigureAwait(false);
                Settings = loaded.Value ?? UserSettings.Default;
            }

            _sessionStartedAt = clock.LocalNow;
            await ReloadDisplaysCoreAsync(cancellationToken).ConfigureAwait(false);
            await RefreshCoreAsync(cancellationToken).ConfigureAwait(false);
            _loopCancellation = new CancellationTokenSource();
            _loopTask = RunLoopAsync(_loopCancellation.Token);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken)
    {
        var cancellation = _loopCancellation;
        var loop = _loopTask;
        if (cancellation is null || loop is null)
        {
            return;
        }

        cancellation.Cancel();
        try
        {
            await loop.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        finally
        {
            cancellation.Dispose();
            _loopCancellation = null;
            _loopTask = null;
        }
    }

    public ValueTask RequestRefreshAsync(CancellationToken cancellationToken) =>
        ExecuteLockedAsync(RefreshCoreAsync, cancellationToken);

    public ValueTask SetAutoEnabledAsync(bool enabled, CancellationToken cancellationToken) =>
        ExecuteLockedAsync(async token =>
        {
            Settings = Settings with { AutoEnabled = enabled };
            if (enabled)
            {
                _pauseUntil = null;
            }

            await configurationCoordinator.SaveAsync(Settings, token).ConfigureAwait(false);
            await RefreshCoreAsync(token).ConfigureAwait(false);
        }, cancellationToken);

    public ValueTask PauseForAsync(TimeSpan duration, CancellationToken cancellationToken) =>
        ExecuteLockedAsync(async token =>
        {
            Settings = Settings with { AutoEnabled = true };
            _pauseUntil = clock.LocalNow.Add(duration);
            await configurationCoordinator.SaveAsync(Settings, token).ConfigureAwait(false);
            await RefreshCoreAsync(token).ConfigureAwait(false);
        }, cancellationToken);

    public ValueTask PauseUntilTomorrowAsync(CancellationToken cancellationToken) =>
        ExecuteLockedAsync(async token =>
        {
            Settings = Settings with { AutoEnabled = true };
            var localNow = clock.LocalNow;
            var tomorrowWake = localNow.Date.AddDays(1).Add(Settings.WakeTime.ToTimeSpan());
            _pauseUntil = new DateTimeOffset(tomorrowWake, localNow.Offset);
            await configurationCoordinator.SaveAsync(Settings, token).ConfigureAwait(false);
            await RefreshCoreAsync(token).ConfigureAwait(false);
        }, cancellationToken);

    public ValueTask ResumeAsync(CancellationToken cancellationToken) =>
        ExecuteLockedAsync(async token =>
        {
            _pauseUntil = null;
            Settings = Settings with { AutoEnabled = true };
            await configurationCoordinator.SaveAsync(Settings, token).ConfigureAwait(false);
            await RefreshCoreAsync(token).ConfigureAwait(false);
        }, cancellationToken);

    public ValueTask ResetDefaultsAsync(CancellationToken cancellationToken) =>
        ExecuteLockedAsync(async token =>
        {
            _pauseUntil = null;
            Settings = UserSettings.Default;
            await configurationCoordinator.SaveAsync(Settings, token).ConfigureAwait(false);
            await ReloadDisplaysCoreAsync(token).ConfigureAwait(false);
            await RefreshCoreAsync(token).ConfigureAwait(false);
        }, cancellationToken);

    public ValueTask ApplySettingsAsync(UserSettings settings, CancellationToken cancellationToken) =>
        ExecuteLockedAsync(async token =>
        {
            Settings = settings;
            await configurationCoordinator.SaveAsync(Settings, token).ConfigureAwait(false);
            await ReloadDisplaysCoreAsync(token).ConfigureAwait(false);
            await RefreshCoreAsync(token).ConfigureAwait(false);
        }, cancellationToken);

    public ValueTask ApplyFeedbackAsync(ComfortFeedback feedback, CancellationToken cancellationToken) =>
        ExecuteLockedAsync(async token =>
        {
            var primary = CurrentSnapshot.PrimaryDecision;
            var result = await feedbackCoordinator.ApplyAsync(
                new FeedbackRequest(
                    feedback,
                    Settings,
                    _displays,
                    CurrentSnapshot.AppContext,
                    CurrentSnapshot.Content,
                    primary?.TargetBrightnessPercent ?? 62,
                    primary?.TargetColorTemperatureKelvin ?? 5200,
                    SessionLength()),
                token).ConfigureAwait(false);
            if (!result.IsUsable)
            {
                return;
            }

            var outcome = result.Value!;
            Settings = outcome.Settings;
            var states = _displays.Select((display, index) => new DisplayRuntimeState(
                display,
                outcome.Decision,
                index < outcome.ApplyResults.Length
                    ? outcome.ApplyResults[index]
                    : BrightnessApplyResult.NoChange(display.Id)));
            CurrentSnapshot = new ComfortRuntimeSnapshot(
                clock.UtcNow,
                CurrentSnapshot.AppContext,
                CurrentSnapshot.Content,
                states,
                outcome.Decision,
                _pauseUntil,
                LearningSummary.From(Settings),
                result.Status == OperationStatus.Degraded
                    ? new SystemHealthState(true, [result.Code ?? "FeedbackDegraded"])
                    : SystemHealthState.Healthy);
            SnapshotChanged?.Invoke(CurrentSnapshot);
        }, cancellationToken);

    public ValueTask ResetComfortAsync(CancellationToken cancellationToken) =>
        ExecuteLockedAsync(async token =>
        {
            _pauseUntil = clock.LocalNow.AddMinutes(30);
            var currentBrightness = CurrentSnapshot.PrimaryDecision?.TargetBrightnessPercent ?? 62;
            var decision = new ComfortDecision(
                ComfortProfileId.Paused,
                currentBrightness,
                6500,
                0,
                TimeSpan.FromSeconds(2),
                true,
                "Comfort cleared",
                ["ComfortCleared"]);
            var states = new List<DisplayRuntimeState>();
            foreach (var display in _displays)
            {
                BrightnessApplyResult applyResult;
                try
                {
                    applyResult = await brightnessController.ApplyAsync(display, decision, Settings, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception)
                {
                    applyResult = new BrightnessApplyResult(display.Id, BrightnessControlLayer.None, BrightnessControlLayer.None, MonitorControlState.Failed, "ApplyFailed");
                }

                states.Add(new DisplayRuntimeState(display, decision, applyResult));
            }

            CurrentSnapshot = new ComfortRuntimeSnapshot(
                clock.UtcNow,
                CurrentSnapshot.AppContext,
                CurrentSnapshot.Content,
                states,
                decision,
                _pauseUntil,
                LearningSummary.From(Settings),
                new SystemHealthState(states.Any(item => item.ApplyResult.State == MonitorControlState.Failed),
                    states.Where(item => item.ApplyResult.State == MonitorControlState.Failed).Select(item => $"Display:{item.Monitor.Id}:ApplyFailed")));
            SnapshotChanged?.Invoke(CurrentSnapshot);
        }, cancellationToken);

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var interval = RefreshCadencePolicy.GetInterval(Settings, _pauseUntil is not null, Settings.EnableContentBrightnessAnalysis);
                await scheduler.DelayAsync(interval, cancellationToken).ConfigureAwait(false);
                await RequestRefreshAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async ValueTask RefreshCoreAsync(CancellationToken cancellationToken)
    {
        if (_pauseUntil <= clock.LocalNow)
        {
            _pauseUntil = null;
        }

        if (_displays.Count == 0)
        {
            await ReloadDisplaysCoreAsync(cancellationToken).ConfigureAwait(false);
        }

        var result = await automationCoordinator.RefreshAsync(
            new ComfortRefreshRequest(Settings, _displays, CurrentSnapshot, _pauseUntil, SessionLength()),
            cancellationToken).ConfigureAwait(false);
        if (result.IsUsable)
        {
            CurrentSnapshot = result.Value!;
            SnapshotChanged?.Invoke(CurrentSnapshot);
        }
    }

    private async ValueTask ReloadDisplaysCoreAsync(CancellationToken cancellationToken)
    {
        var result = await displayLifecycleCoordinator.LoadAsync(Settings, cancellationToken).ConfigureAwait(false);
        _displays = result.Value ?? Array.Empty<MonitorModel>();
    }

    private TimeSpan SessionLength() => clock.LocalNow - _sessionStartedAt;

    private async ValueTask ExecuteLockedAsync(Func<CancellationToken, ValueTask> operation, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await operation(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }
}
