using Aptema.Core;

namespace Aptema.Application;

public sealed class DisplayLifecycleCoordinator(
    IMonitorEnumerator monitorEnumerator,
    IDisplayTopologyObserver? topologyObserver = null)
{
    public async ValueTask<OperationResult<IReadOnlyList<MonitorModel>>> LoadAsync(UserSettings settings, CancellationToken cancellationToken)
    {
        try
        {
            var monitors = await monitorEnumerator.EnumerateAsync(cancellationToken).ConfigureAwait(false);
            var configured = ApplyPreferences(monitors, settings);
            if (topologyObserver is not null)
            {
                await topologyObserver.UpdateTopologyAsync(configured, cancellationToken).ConfigureAwait(false);
            }

            return OperationResult<IReadOnlyList<MonitorModel>>.Succeeded(configured);
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
            var aliases = monitor.Aliases.Append(monitor.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var configuration = settings.DisplayConfigurations.FirstOrDefault(item =>
                aliases.Contains(item.StableId) || item.LegacyAliases.Any(aliases.Contains));
            if (configuration is not null)
            {
                return monitor with
                {
                    BrightnessOffsetPercent = configuration.BrightnessOffsetPercent,
                    MinimumBrightnessPercent = Math.Clamp(configuration.MinimumBrightnessPercent, 15, 100),
                    MaximumBrightnessPercent = Math.Clamp(configuration.MaximumBrightnessPercent, configuration.MinimumBrightnessPercent, 100)
                };
            }

            var preference = settings.MonitorPreferences.FirstOrDefault(item => aliases.Contains(item.MonitorId));
            return preference is null ? monitor : monitor with { BrightnessOffsetPercent = preference.BrightnessOffsetPercent };
        }).ToArray();
    }
}
