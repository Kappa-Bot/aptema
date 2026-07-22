using Aptema.Application;
using Aptema.Core;

namespace Aptema.Infrastructure;

public sealed class BrightnessController : IBrightnessController, IUndoBrightnessController
{
    private static readonly TimeSpan MinimumWriteInterval = TimeSpan.FromSeconds(2);

    private readonly IDdcCiApi _ddcCiApi;
    private readonly IWindowsBrightnessApi _windowsBrightnessApi;
    private readonly IOverlayController _overlayController;
    private readonly TimeProvider _timeProvider;
    private readonly IAdaptiveScheduler _scheduler;
    private readonly Dictionary<string, DateTimeOffset> _lastWrites = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DdcFailureState> _ddcFailures = new(StringComparer.OrdinalIgnoreCase);

    public BrightnessController(
        IDdcCiApi ddcCiApi,
        IWindowsBrightnessApi windowsBrightnessApi,
        IOverlayController overlayController,
        TimeProvider? timeProvider = null,
        IAdaptiveScheduler? scheduler = null)
    {
        _ddcCiApi = ddcCiApi;
        _windowsBrightnessApi = windowsBrightnessApi;
        _overlayController = overlayController;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _scheduler = scheduler ?? new TimeProviderAdaptiveScheduler(_timeProvider);
    }

    public async ValueTask<BrightnessApplyResult> ApplyAsync(MonitorModel monitor, ComfortDecision decision, UserSettings settings, CancellationToken cancellationToken)
    {
        if (IsMonitorDisabled(monitor, settings))
        {
            return Result(monitor, BrightnessControlLayer.None, BrightnessControlLayer.None, MonitorControlState.Disabled, "MonitorDisabled");
        }

        if (!decision.ShouldApply)
        {
            return decision.Source == DecisionSource.Protected
                ? Result(monitor, BrightnessControlLayer.None, BrightnessControlLayer.None, MonitorControlState.Protected, "ProtectedContext")
                : Result(monitor, BrightnessControlLayer.None, BrightnessControlLayer.None, MonitorControlState.NoChange, "DecisionSuppressed");
        }

        var now = _timeProvider.GetUtcNow();
        if (_lastWrites.TryGetValue(monitor.Id, out var lastWrite) && now - lastWrite < MinimumWriteInterval)
        {
            return Result(
                monitor,
                BrightnessControlLayer.None,
                BrightnessControlLayer.None,
                MonitorControlState.Throttled,
                "WriteThrottled",
                lastWrite + MinimumWriteInterval);
        }

        var brightness = ClampBrightness(monitor, settings, decision.TargetBrightnessPercent);
        var preferred = settings.EnableDdcCi && monitor.SupportsBrightnessControl
            ? BrightnessControlLayer.DdcCi
            : BrightnessControlLayer.WindowsBrightness;
        var failedHardware = false;
        var ddcState = _ddcFailures.GetValueOrDefault(monitor.Id);
        var ddcSuppressed = ddcState is not null && ddcState.SuppressedUntil > now;

        if (settings.EnableDdcCi && monitor.SupportsBrightnessControl && !ddcSuppressed)
        {
            try
            {
                if (await _ddcCiApi.TrySetBrightnessAsync(monitor, brightness, cancellationToken).ConfigureAwait(false))
                {
                    _ddcFailures.Remove(monitor.Id);
                    if (!await TryApplyOverlayAsync(monitor, decision, cancellationToken).ConfigureAwait(false))
                    {
                        _lastWrites[monitor.Id] = now;
                        return Result(monitor, preferred, BrightnessControlLayer.DdcCi, MonitorControlState.Failed, "OverlayControlFailed", usedHardware: true);
                    }

                    _lastWrites[monitor.Id] = now;
                    return Result(monitor, preferred, BrightnessControlLayer.DdcCi, MonitorControlState.Ready, "DdcCiApplied", usedHardware: true, usedOverlay: decision.OverlayOpacity > 0);
                }

                ddcState = RecordDdcFailure(monitor.Id, now);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                failedHardware = true;
                ddcState = RecordDdcFailure(monitor.Id, now);
            }
        }

        try
        {
            if (await _windowsBrightnessApi.TrySetBrightnessAsync(monitor, brightness, cancellationToken).ConfigureAwait(false))
            {
                if (!await TryApplyOverlayAsync(monitor, decision, cancellationToken).ConfigureAwait(false))
                {
                    _lastWrites[monitor.Id] = now;
                    return Result(monitor, preferred, BrightnessControlLayer.WindowsBrightness, MonitorControlState.Failed, "OverlayControlFailed", ddcState?.SuppressedUntil, usedHardware: true);
                }

                _lastWrites[monitor.Id] = now;
                var state = ddcSuppressed || (ddcState?.SuppressedUntil > now)
                    ? MonitorControlState.Degraded
                    : preferred == BrightnessControlLayer.DdcCi
                        ? MonitorControlState.FallbackUsed
                        : MonitorControlState.Ready;
                return Result(monitor, preferred, BrightnessControlLayer.WindowsBrightness, state, ReasonFor(state), ddcState?.SuppressedUntil, usedHardware: true, usedOverlay: decision.OverlayOpacity > 0);
            }
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            failedHardware = true;
        }

        if (FindDisplayConfiguration(monitor, settings) is { AllowSoftwareFallback: false })
        {
            _lastWrites[monitor.Id] = now;
            return Result(monitor, preferred, BrightnessControlLayer.None, MonitorControlState.Unsupported, "SoftwareFallbackDisabled", ddcState?.SuppressedUntil);
        }

        if (!await TryApplyOverlayAsync(monitor, decision, cancellationToken).ConfigureAwait(false))
        {
            _lastWrites[monitor.Id] = now;
            return Result(monitor, preferred, BrightnessControlLayer.Overlay, MonitorControlState.Failed, "OverlayControlFailed", ddcState?.SuppressedUntil);
        }

        _lastWrites[monitor.Id] = now;
        return Result(
            monitor,
            preferred,
            BrightnessControlLayer.Overlay,
            failedHardware ? MonitorControlState.Failed : MonitorControlState.FallbackUsed,
            failedHardware ? "HardwareControlFailed" : "OverlayFallbackApplied",
            ddcState?.SuppressedUntil,
            usedHardware: false,
            usedOverlay: true);
    }

    public async ValueTask<BrightnessApplyResult> ApplyUndoAsync(
        MonitorModel monitor,
        ComfortDecision decision,
        UserSettings settings,
        DateTimeOffset? retryAfter,
        CancellationToken cancellationToken)
    {
        if (IsMonitorDisabled(monitor, settings))
        {
            return Result(monitor, BrightnessControlLayer.None, BrightnessControlLayer.None, MonitorControlState.Disabled, "MonitorDisabled");
        }

        var nextAttempt = retryAfter;
        if (nextAttempt is null && _lastWrites.TryGetValue(monitor.Id, out var lastWrite))
        {
            nextAttempt = lastWrite + MinimumWriteInterval;
        }

        await WaitUntilAsync(nextAttempt, cancellationToken).ConfigureAwait(false);
        var result = await ApplyAsync(monitor, decision, settings, cancellationToken).ConfigureAwait(false);
        if (result.State != MonitorControlState.Throttled)
        {
            return result;
        }

        await WaitUntilAsync(result.SuppressedUntil, cancellationToken).ConfigureAwait(false);
        return await ApplyAsync(monitor, decision, settings, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask WaitUntilAsync(DateTimeOffset? requestedAt, CancellationToken cancellationToken)
    {
        if (requestedAt is not { } retryAt)
        {
            return;
        }

        var now = _timeProvider.GetUtcNow();
        var delay = retryAt - now;
        if (delay <= TimeSpan.Zero)
        {
            return;
        }

        await _scheduler.DelayAsync(delay < MinimumWriteInterval ? delay : MinimumWriteInterval, cancellationToken).ConfigureAwait(false);
    }

    private static bool IsMonitorDisabled(MonitorModel monitor, UserSettings settings)
    {
        if (FindDisplayConfiguration(monitor, settings) is { } configuration)
        {
            return !configuration.IsEnabled;
        }

        return settings.MonitorPreferences.Any(preference =>
            string.Equals(preference.MonitorId, monitor.Id, StringComparison.OrdinalIgnoreCase) &&
            preference.IsDisabled);
    }

    private static int ClampBrightness(MonitorModel monitor, UserSettings settings, int brightness)
    {
        var configuration = FindDisplayConfiguration(monitor, settings);
        var preference = settings.MonitorPreferences.FirstOrDefault(item =>
            string.Equals(item.MonitorId, monitor.Id, StringComparison.OrdinalIgnoreCase));
        var minimum = Math.Max(15, Math.Max(monitor.MinimumBrightnessPercent, configuration?.MinimumBrightnessPercent ?? preference?.MinimumBrightnessPercent ?? settings.MinimumBrightnessPercent));
        var maximum = Math.Min(monitor.MaximumBrightnessPercent, configuration?.MaximumBrightnessPercent ?? preference?.MaximumBrightnessPercent ?? settings.MaximumBrightnessPercent);
        if (minimum > maximum)
        {
            minimum = maximum;
        }

        return Math.Clamp(brightness, minimum, maximum);
    }

    private static DisplayConfiguration? FindDisplayConfiguration(MonitorModel monitor, UserSettings settings)
    {
        var identities = monitor.Aliases.Append(monitor.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return settings.DisplayConfigurations.FirstOrDefault(item =>
            identities.Contains(item.StableId) || item.LegacyAliases.Any(identities.Contains));
    }

    private async ValueTask<bool> TryApplyOverlayAsync(MonitorModel monitor, ComfortDecision decision, CancellationToken cancellationToken)
    {
        try
        {
            await _overlayController.ApplyAsync(monitor, decision.OverlayOpacity, decision.TargetColorTemperatureKelvin, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    private DdcFailureState RecordDdcFailure(string monitorId, DateTimeOffset now)
    {
        var count = _ddcFailures.TryGetValue(monitorId, out var current) ? current.Count + 1 : 1;
        var suppressedUntil = count >= 3 ? now.AddMinutes(10) : now;
        var state = new DdcFailureState(count, suppressedUntil);
        _ddcFailures[monitorId] = state;
        return state;
    }

    private sealed record DdcFailureState(int Count, DateTimeOffset SuppressedUntil);

    private static BrightnessApplyResult Result(
        MonitorModel monitor,
        BrightnessControlLayer preferred,
        BrightnessControlLayer applied,
        MonitorControlState state,
        string reasonCode,
        DateTimeOffset? suppressedUntil = null,
        bool usedHardware = false,
        bool usedOverlay = false)
    {
        return new BrightnessApplyResult(monitor.Id, preferred, applied, state, reasonCode, suppressedUntil, usedHardware, usedOverlay);
    }

    private static string ReasonFor(MonitorControlState state)
    {
        return state switch
        {
            MonitorControlState.Degraded => "DdcCiBackoff",
            MonitorControlState.FallbackUsed => "HardwareFallbackApplied",
            _ => "BrightnessApplied"
        };
    }
}
