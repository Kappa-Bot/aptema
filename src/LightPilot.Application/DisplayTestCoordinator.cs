using LightPilot.Core;

namespace LightPilot.Application;

public sealed class DisplayTestCoordinator(IBrightnessController brightnessController, IAdaptiveScheduler scheduler)
{
    public async ValueTask<OperationResult<BrightnessApplyResult>> TestAsync(
        MonitorModel monitor,
        ComfortDecision baseline,
        UserSettings settings,
        CancellationToken cancellationToken)
    {
        var test = baseline with
        {
            TargetBrightnessPercent = Math.Max(settings.MinimumBrightnessPercent, baseline.TargetBrightnessPercent - 3),
            OverlayOpacity = Math.Max(0.04, baseline.OverlayOpacity),
            TransitionDuration = TimeSpan.FromMilliseconds(600),
            ShouldApply = true,
            Source = DecisionSource.Manual,
            Reason = "Display check"
        };
        var applied = await brightnessController.ApplyAsync(monitor, test, settings, cancellationToken).ConfigureAwait(false);
        if (applied.State is MonitorControlState.Failed or MonitorControlState.Unsupported)
        {
            return OperationResult<BrightnessApplyResult>.Failure(OperationStatus.Unavailable, "DisplayTestUnavailable");
        }

        await scheduler.DelayAsync(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
        var restore = baseline with { ShouldApply = true, Source = DecisionSource.Manual, Reason = "Display check restored" };
        var restored = brightnessController is IUndoBrightnessController undo
            ? await undo.ApplyUndoAsync(monitor, restore, settings, applied.SuppressedUntil, cancellationToken).ConfigureAwait(false)
            : await brightnessController.ApplyAsync(monitor, restore, settings, cancellationToken).ConfigureAwait(false);

        return restored.State is MonitorControlState.Failed or MonitorControlState.Unsupported
            ? OperationResult<BrightnessApplyResult>.Degraded(restored, "DisplayTestRestoreFailed")
            : OperationResult<BrightnessApplyResult>.Succeeded(restored);
    }
}
