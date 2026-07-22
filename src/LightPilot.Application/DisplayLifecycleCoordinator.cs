using LightPilot.Core;

namespace LightPilot.Application;

public sealed class DisplayLifecycleCoordinator(IMonitorEnumerator monitorEnumerator)
{
    public async ValueTask<OperationResult<IReadOnlyList<MonitorModel>>> LoadAsync(UserSettings settings, CancellationToken cancellationToken)
    {
        try
        {
            var monitors = await monitorEnumerator.EnumerateAsync(cancellationToken).ConfigureAwait(false);
            return OperationResult<IReadOnlyList<MonitorModel>>.Succeeded(ApplyPreferences(monitors, settings));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            MonitorModel[] fallback = [new("primary", "Primary display", false, true, 15, 100, 0)];
            return OperationResult<IReadOnlyList<MonitorModel>>.Degraded(fallback, "DisplayEnumerationUnavailable");
        }
    }

    private static IReadOnlyList<MonitorModel> ApplyPreferences(IReadOnlyList<MonitorModel> monitors, UserSettings settings)
    {
        return monitors.Select(monitor =>
        {
            var preference = settings.MonitorPreferences.FirstOrDefault(item =>
                string.Equals(item.MonitorId, monitor.Id, StringComparison.OrdinalIgnoreCase));
            return preference is null
                ? monitor
                : monitor with { BrightnessOffsetPercent = preference.BrightnessOffsetPercent };
        }).ToArray();
    }
}
