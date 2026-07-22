using LightPilot.Core;

namespace LightPilot.Application;

public sealed class ComfortAutomationCoordinator(
    IForegroundWindowDetector foregroundWindowDetector,
    IContentLuminanceSampler contentLuminanceSampler,
    IBrightnessController brightnessController,
    IPowerStatusProvider powerStatusProvider,
    IClock clock,
    ILatestValueChannel<ComfortContextUpdate> contextUpdates)
{
    private readonly AdaptiveEngine _engine = new();
    private readonly Dictionary<string, AdaptiveEngineState> _states = new(StringComparer.OrdinalIgnoreCase);

    public async ValueTask<OperationResult<ComfortRuntimeSnapshot>> RefreshAsync(ComfortRefreshRequest request, CancellationToken cancellationToken)
    {
        var issues = new List<string>();
        var settings = PowerAwareSettingsPolicy.Apply(request.Settings, powerStatusProvider.IsOnBattery());
        var now = clock.LocalNow;
        var pausedUntil = request.PauseUntil > now ? request.PauseUntil : null;
        if (pausedUntil is not null)
        {
            settings = settings with { AutoEnabled = false };
        }

        var appContext = settings.AutoEnabled ? DetectContext(issues) : new AppContextModel("LightPilot.App", AppCategory.System, false);
        appContext = ApplyOverride(appContext, settings);
        var shouldSample = settings.EnableContentBrightnessAnalysis &&
            appContext.Category is AppCategory.Browser or AppCategory.EmailCommunication or AppCategory.OfficeReading;
        var content = settings.AutoEnabled
            ? await SampleContentAsync(shouldSample, issues, cancellationToken).ConfigureAwait(false)
            : ContentLuminanceSample.Unknown;
        contextUpdates.TryPublish(new ComfortContextUpdate(clock.UtcNow, appContext, content));

        var displays = new List<DisplayRuntimeState>();
        foreach (var monitor in request.Displays)
        {
            var previousDisplay = request.Previous?.Displays.FirstOrDefault(item =>
                string.Equals(item.Monitor.Id, monitor.Id, StringComparison.OrdinalIgnoreCase));
            var currentBrightness = previousDisplay?.Decision.TargetBrightnessPercent ?? 62;
            var currentKelvin = previousDisplay?.Decision.TargetColorTemperatureKelvin ?? 5200;
            var snapshot = new AdaptiveSnapshot(now, monitor, appContext, content, request.ScreenSessionLength, currentBrightness, currentKelvin, pausedUntil);
            var state = _states.GetValueOrDefault(monitor.Id, AdaptiveEngineState.Empty);
            var decision = _engine.Evaluate(snapshot, state, settings);
            BrightnessApplyResult applyResult;

            if (IsDisabled(monitor, settings))
            {
                applyResult = new BrightnessApplyResult(monitor.Id, BrightnessControlLayer.None, BrightnessControlLayer.None, MonitorControlState.Disabled, "MonitorDisabled");
            }
            else
            {
                try
                {
                    applyResult = await brightnessController.ApplyAsync(monitor, decision, settings, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception)
                {
                    applyResult = new BrightnessApplyResult(monitor.Id, BrightnessControlLayer.None, BrightnessControlLayer.None, MonitorControlState.Failed, "ApplyFailed");
                }
            }

            if (applyResult.State is MonitorControlState.Degraded or MonitorControlState.Failed or MonitorControlState.Unsupported)
            {
                issues.Add($"Display:{monitor.Id}:{applyResult.ReasonCode}");
            }

            if (decision.ShouldApply)
            {
                _states[monitor.Id] = state with { LastAppliedAt = now, LastDecision = decision.Target };
            }

            displays.Add(new DisplayRuntimeState(monitor, decision, applyResult));
        }

        if (displays.Count == 0)
        {
            issues.Add("NoDisplaysAvailable");
        }

        var runtime = new ComfortRuntimeSnapshot(
            clock.UtcNow,
            appContext,
            content,
            displays,
            displays.FirstOrDefault()?.Decision,
            pausedUntil,
            LearningSummary.From(request.Settings),
            new SystemHealthState(issues.Count > 0, issues));
        return issues.Count == 0
            ? OperationResult<ComfortRuntimeSnapshot>.Succeeded(runtime)
            : OperationResult<ComfortRuntimeSnapshot>.Degraded(runtime, "ComfortRuntimeDegraded");
    }

    private AppContextModel DetectContext(List<string> issues)
    {
        try
        {
            return foregroundWindowDetector.Detect();
        }
        catch (Exception)
        {
            issues.Add("ContextDetectionUnavailable");
            return new AppContextModel("unknown.exe", AppCategory.Unknown, false);
        }
    }

    private async ValueTask<ContentLuminanceSample> SampleContentAsync(bool enabled, List<string> issues, CancellationToken cancellationToken)
    {
        try
        {
            return await contentLuminanceSampler.SampleAsync(enabled, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            issues.Add("ContentSamplingUnavailable");
            return ContentLuminanceSample.Unknown;
        }
    }

    private static AppContextModel ApplyOverride(AppContextModel context, UserSettings settings)
    {
        if (string.IsNullOrWhiteSpace(context.ProcessName))
        {
            return context;
        }

        var category = AppCategoryMapper.CreateDefault(settings.AppOverrides).Classify(context.ProcessName);
        return category == context.Category ? context : context with { Category = category };
    }

    private static bool IsDisabled(MonitorModel monitor, UserSettings settings)
    {
        return settings.MonitorPreferences.Any(item => item.IsDisabled &&
            string.Equals(item.MonitorId, monitor.Id, StringComparison.OrdinalIgnoreCase));
    }
}
