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
}
