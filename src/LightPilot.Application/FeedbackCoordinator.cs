using LightPilot.Core;

namespace LightPilot.Application;

public sealed class FeedbackCoordinator(
    ISettingsStore settingsStore,
    IBrightnessController brightnessController,
    IClock clock)
{
    private readonly AdaptiveEngine _engine = new();

    public async ValueTask<OperationResult<FeedbackOutcome>> ApplyAsync(FeedbackRequest request, CancellationToken cancellationToken)
    {
        var monitor = request.Displays.FirstOrDefault()
            ?? new MonitorModel("primary", "Primary display", false, true, 15, 100, 0);
        var snapshot = new AdaptiveSnapshot(
            clock.LocalNow,
            monitor,
            request.AppContext,
            request.Content,
            request.ScreenSessionLength,
            request.CurrentBrightness,
            request.CurrentColorTemperatureKelvin,
            null);
        var context = PreferenceLearningContext.FromSnapshot(snapshot);
        var updatedSettings = ComfortPreferenceAdvisor.Apply(request.Settings, request.Feedback, context, clock.UtcNow);
        var decision = _engine.EvaluateManualFeedback(snapshot, updatedSettings, request.Feedback);
        var results = new List<BrightnessApplyResult>();
        var degraded = false;

        foreach (var display in request.Displays)
        {
            try
            {
                results.Add(await brightnessController.ApplyAsync(display, decision, updatedSettings, cancellationToken).ConfigureAwait(false));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                degraded = true;
                results.Add(new BrightnessApplyResult(display.Id, BrightnessControlLayer.None, BrightnessControlLayer.None, MonitorControlState.Failed, "ApplyFailed"));
            }
        }

        try
        {
            await settingsStore.SaveAsync(updatedSettings, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            degraded = true;
        }

        var outcome = new FeedbackOutcome(updatedSettings, decision, results);
        return degraded
            ? OperationResult<FeedbackOutcome>.Degraded(outcome, "FeedbackPartiallyApplied")
            : OperationResult<FeedbackOutcome>.Succeeded(outcome);
    }

    public async ValueTask<OperationResult<FeedbackOutcome>> UndoAsync(FeedbackUndoRequest request, CancellationToken cancellationToken)
    {
        if (!await TrySaveSettingsAsync(request.PreviousSettings, cancellationToken).ConfigureAwait(false))
        {
            return OperationResult<FeedbackOutcome>.Failure(OperationStatus.Unavailable, "FeedbackUndoPersistenceUnavailable");
        }

        var results = new List<BrightnessApplyResult>();
        var attemptedDisplays = new List<MonitorModel>();
        foreach (var display in request.Displays)
        {
            attemptedDisplays.Add(display);
            var result = await ApplyTargetAsync(
                display,
                request.PreviousDecision,
                request.PreviousSettings,
                cancellationToken).ConfigureAwait(false);
            results.Add(result);
            if (!IsConsistentApply(result))
            {
                return await RestorePostFeedbackStateAsync(request, attemptedDisplays, cancellationToken).ConfigureAwait(false);
            }
        }

        var outcome = new FeedbackOutcome(request.PreviousSettings, request.PreviousDecision, results);
        return OperationResult<FeedbackOutcome>.Succeeded(outcome);
    }

    private async ValueTask<OperationResult<FeedbackOutcome>> RestorePostFeedbackStateAsync(
        FeedbackUndoRequest request,
        IReadOnlyList<MonitorModel> attemptedDisplays,
        CancellationToken cancellationToken)
    {
        var settingsRestored = await TrySaveSettingsAsync(request.PostFeedbackSettings, cancellationToken).ConfigureAwait(false);
        var compensationFailed = false;
        foreach (var display in attemptedDisplays)
        {
            var result = await ApplyTargetAsync(
                display,
                request.PostFeedbackDecision,
                request.PostFeedbackSettings,
                cancellationToken).ConfigureAwait(false);
            compensationFailed |= !IsConsistentApply(result);
        }

        if (compensationFailed)
        {
            return OperationResult<FeedbackOutcome>.Failure(OperationStatus.Degraded, "FeedbackUndoCompensationFailed");
        }

        return settingsRestored
            ? OperationResult<FeedbackOutcome>.Failure(OperationStatus.Unavailable, "FeedbackUndoCompensated")
            : OperationResult<FeedbackOutcome>.Failure(OperationStatus.Degraded, "FeedbackUndoRollbackPersistenceFailed");
    }

    private async ValueTask<BrightnessApplyResult> ApplyTargetAsync(
        MonitorModel display,
        ComfortDecision decision,
        UserSettings settings,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await brightnessController.ApplyAsync(display, decision, settings, cancellationToken).ConfigureAwait(false);
            if (!IsConsistentApply(result) &&
                result.State != MonitorControlState.Disabled &&
                brightnessController is IUndoBrightnessController undoController)
            {
                result = await undoController.ApplyUndoAsync(
                    display,
                    decision,
                    settings,
                    result.SuppressedUntil,
                    cancellationToken).ConfigureAwait(false);
            }

            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return new BrightnessApplyResult(
                display.Id,
                BrightnessControlLayer.None,
                BrightnessControlLayer.None,
                MonitorControlState.Failed,
                "ApplyFailed");
        }
    }

    private async ValueTask<bool> TrySaveSettingsAsync(UserSettings settings, CancellationToken cancellationToken)
    {
        try
        {
            await settingsStore.SaveAsync(settings, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool IsConsistentApply(BrightnessApplyResult result) =>
        result.State == MonitorControlState.Disabled ||
        result.State is MonitorControlState.Ready or MonitorControlState.FallbackUsed or MonitorControlState.Degraded &&
        (result.UsedHardware || result.UsedOverlay);
}
